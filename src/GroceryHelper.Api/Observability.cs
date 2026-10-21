using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Routing;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Compact;

internal static class Observability
{
    public static void AddObservability(this WebApplicationBuilder builder)
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

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

        var serviceVersion = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var openTelemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: "GroceryHelper.Api",
                    serviceVersion: serviceVersion)
                .AddAttributes([
                    new KeyValuePair<string, object>(
                        "deployment.environment.name",
                        environment.EnvironmentName),
                ]))
            .WithTracing(tracing => tracing
                .SetSampler(environment.IsDevelopment()
                    ? new AlwaysOnSampler()
                    : new ParentBasedSampler(new AlwaysOnSampler()))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddProcessor(new PrivacyFilteringActivityProcessor()))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());
        var consoleExporterEnabled = configuration.GetValue<bool?>(
            "Observability:OpenTelemetry:ConsoleExporter:Enabled")
            ?? environment.IsDevelopment();
        if (consoleExporterEnabled)
        {
            openTelemetry
                .WithTracing(tracing => tracing.AddConsoleExporter())
                .WithMetrics(metrics => metrics.AddConsoleExporter());
        }

        var configuredFlushTimeout = configuration.GetValue<int?>(
            "Observability:OpenTelemetry:FlushTimeoutMilliseconds");
        var flushTimeout = TimeSpan.FromMilliseconds(
            configuredFlushTimeout is >= 0 and <= 30_000
                ? configuredFlushTimeout.Value
                : 5_000);
        builder.Services.AddSingleton<IHostedService>(services =>
            new TelemetryLifetime(
                services.GetRequiredService<TracerProvider>(),
                services.GetRequiredService<MeterProvider>(),
                flushTimeout));
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

internal sealed class TelemetryLifetime(
    TracerProvider tracerProvider,
    MeterProvider meterProvider,
    TimeSpan flushTimeout) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        TryFlush(tracerProvider.ForceFlush, RemainingMilliseconds(started));
        TryFlush(meterProvider.ForceFlush, RemainingMilliseconds(started));
        return Task.CompletedTask;
    }

    private int RemainingMilliseconds(long started)
    {
        var remaining = flushTimeout - Stopwatch.GetElapsedTime(started);
        return Math.Max(0, (int)Math.Ceiling(remaining.TotalMilliseconds));
    }

    private static void TryFlush(
        Func<int, bool> forceFlush,
        int timeoutMilliseconds)
    {
        try
        {
            forceFlush(timeoutMilliseconds);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine(
                "Telemetry flush did not complete ({0}).",
                exception.GetType().Name);
        }
    }
}

internal sealed class PrivacyFilteringActivityProcessor : BaseProcessor<Activity>
{
    private static readonly string[] QueryBearingTags =
        ["url.query", "url.full", "http.target"];

    public override void OnEnd(Activity activity)
    {
        foreach (var tagName in QueryBearingTags)
        {
            activity.SetTag(tagName, value: null);
        }
    }
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
