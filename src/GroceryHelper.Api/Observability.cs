using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Compact;

internal static class Observability
{
    private const long DefaultFileSizeLimitBytes = 10 * 1024 * 1024;
    private const int DefaultRetainedFileCount = 7;

    public static void AddObservability(this WebApplicationBuilder builder)
    {
        SelfLog.Enable(TextWriter.Synchronized(Console.Error));

        builder.Services.AddSerilog((services, configuration) =>
        {
            var applicationConfiguration = services.GetRequiredService<IConfiguration>();
            var environment = services.GetRequiredService<IHostEnvironment>();
            var minimumLevel = GetLevel(
                applicationConfiguration,
                "Observability:MinimumLevel",
                LogEventLevel.Information);
            var frameworkMinimumLevel = GetLevel(
                applicationConfiguration,
                "Observability:FrameworkMinimumLevel",
                LogEventLevel.Warning);
            var hostingLifetimeMinimumLevel = GetLevel(
                applicationConfiguration,
                "Observability:HostingLifetimeMinimumLevel",
                LogEventLevel.Information);
            var fileEnabled = applicationConfiguration.GetValue<bool?>("Observability:File:Enabled")
                ?? environment.IsDevelopment();
            var configuredPath = applicationConfiguration["Observability:File:Path"]
                ?? Path.Combine("logs", "grocery-helper-.ndjson");
            var filePath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath);
            var fileSizeLimitBytes = applicationConfiguration.GetValue<long?>(
                "Observability:File:SizeLimitBytes") ?? DefaultFileSizeLimitBytes;
            var retainedFileCount = applicationConfiguration.GetValue<int?>(
                "Observability:File:RetainedFileCount") ?? DefaultRetainedFileCount;

            configuration
                .MinimumLevel.Is(minimumLevel)
                .MinimumLevel.Override("Microsoft", frameworkMinimumLevel)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", hostingLifetimeMinimumLevel)
                .Enrich.FromLogContext()
                .WriteTo.Console(new CompactJsonFormatter());

            if (fileEnabled)
            {
                try
                {
                    configuration.WriteTo.File(
                        new CompactJsonFormatter(),
                        filePath,
                        rollingInterval: RollingInterval.Day,
                        fileSizeLimitBytes: fileSizeLimitBytes,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: retainedFileCount,
                        shared: true);
                }
                catch (Exception exception) when (
                    exception is ArgumentException or IOException or UnauthorizedAccessException
                        or NotSupportedException or System.Security.SecurityException)
                {
                    Console.Error.WriteLine(
                        "File logging is disabled because its configuration or destination " +
                        "is unavailable ({0}).",
                        exception.GetType().Name);
                }
            }
        });
    }

    public static IApplicationBuilder UseRequestObservability(this WebApplication app) =>
        app.UseMiddleware<RequestObservabilityMiddleware>();

    private static LogEventLevel GetLevel(
        IConfiguration configuration,
        string key,
        LogEventLevel defaultLevel) =>
        Enum.TryParse<LogEventLevel>(
            configuration[key],
            ignoreCase: true,
            out var configuredLevel)
            ? configuredLevel
            : defaultLevel;
}

internal sealed class RequestObservabilityMiddleware(
    RequestDelegate next,
    ILogger<RequestObservabilityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var statusCode = StatusCodes.Status500InternalServerError;

        try
        {
            await next(context);
            statusCode = context.Response.StatusCode;
        }
        finally
        {
            var routeTemplate = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                ?? "unmatched";
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var traceId = Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
            var level = GetLevel(routeTemplate, statusCode);

            logger.Log(
                level,
                "HTTP request completed: {EventName} {RequestMethod} {RouteTemplate} " +
                "{StatusCode} {ElapsedMilliseconds} {TraceId}",
                "HttpRequestCompleted",
                context.Request.Method,
                routeTemplate,
                statusCode,
                elapsed,
                traceId);
        }
    }

    private static LogLevel GetLevel(string routeTemplate, int statusCode)
    {
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogLevel.Error;
        }

        if (statusCode >= StatusCodes.Status400BadRequest)
        {
            return LogLevel.Warning;
        }

        return routeTemplate == "/health" ? LogLevel.Debug : LogLevel.Information;
    }
}
