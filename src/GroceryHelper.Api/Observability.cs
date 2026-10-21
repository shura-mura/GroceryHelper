using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Compact;

internal static class Observability
{
    public static void AddObservability(this WebApplicationBuilder builder)
    {
        SelfLog.Enable(TextWriter.Synchronized(Console.Error));
        var environment = builder.Environment;
        var configuration = builder.Configuration;
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(GetLevel(
                configuration["Observability:MinimumLevel"],
                LogEventLevel.Information))
            .MinimumLevel.Override("Microsoft", GetLevel(
                configuration["Observability:FrameworkMinimumLevel"],
                LogEventLevel.Warning))
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", GetLevel(
                configuration["Observability:HostingLifetimeMinimumLevel"],
                LogEventLevel.Information))
            .Enrich.FromLogContext()
            .WriteTo.Console(new CompactJsonFormatter());
        var fileEnabled = configuration.GetValue<bool?>("Observability:File:Enabled")
            ?? environment.IsDevelopment();

        if (fileEnabled)
        {
            var configuredPath = configuration["Observability:File:Path"]
                ?? Path.Combine("logs", "grocery-helper-.ndjson");
            var filePath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath);
            var rollingInterval = Enum.TryParse<RollingInterval>(
                configuration["Observability:File:RollingInterval"],
                ignoreCase: true,
                out var configuredRollingInterval)
                ? configuredRollingInterval
                : RollingInterval.Day;

            try
            {
                loggerConfiguration.WriteTo.File(
                    new CompactJsonFormatter(),
                    filePath,
                    rollingInterval: rollingInterval,
                    fileSizeLimitBytes: configuration.GetValue<long?>(
                        "Observability:File:SizeLimitBytes") ?? 10 * 1024 * 1024,
                    rollOnFileSizeLimit: configuration.GetValue<bool?>(
                        "Observability:File:RollOnFileSizeLimit") ?? true,
                    retainedFileCountLimit: configuration.GetValue<int?>(
                        "Observability:File:RetainedFileCount") ?? 7);
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

        var logger = loggerConfiguration.CreateLogger();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(logger, dispose: false);
        builder.Services.AddSingleton<IHostedService>(services =>
            new LoggerLifetime(
                (IDisposable)logger,
                services.GetRequiredService<IHostApplicationLifetime>()));
    }

    public static IApplicationBuilder UseRequestObservability(this WebApplication app) =>
        app.UseMiddleware<RequestObservabilityMiddleware>();

    private static LogEventLevel GetLevel(
        string? levelName,
        LogEventLevel defaultLevel) =>
        Enum.TryParse<LogEventLevel>(
            levelName,
            ignoreCase: true,
            out var configuredLevel)
            ? configuredLevel
            : defaultLevel;
}

internal sealed class LoggerLifetime : IHostedService, IDisposable
{
    private IDisposable? logger;
    private IDisposable? registration;

    public LoggerLifetime(
        IDisposable logger,
        IHostApplicationLifetime applicationLifetime)
    {
        this.logger = logger;
        registration = applicationLifetime.ApplicationStopped.Register(DisposeLogger);
    }

    public Task StartAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose()
    {
        registration?.Dispose();
        registration = null;
        DisposeLogger();
    }

    private void DisposeLogger() => Interlocked.Exchange(ref logger, null)?.Dispose();
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
            var traceId = Activity.Current?.TraceId.ToHexString()
                ?? ActivityTraceId.CreateRandom().ToHexString();
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
