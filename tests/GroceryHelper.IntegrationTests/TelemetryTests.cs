using System.Collections;
using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace GroceryHelper.IntegrationTests;

[NotInParallel]
public class TelemetryTests
{
    [Test]
    public async Task Request_exports_a_privacy_safe_server_span_and_http_metrics()
    {
        // Arrange
        const string secret = "sentinel-secret-value";
        const string traceId = "0af7651916cd43dd8448eb211c80319c";
        var activities = new ConcurrentCollection<Activity>();
        var metrics = new ConcurrentCollection<Metric>();
        await using var factory = CreateFactory(activities, metrics);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "traceparent",
            $"00-{traceId}-b7ad6b7169203331-01");

        // Act
        using var response = await client.GetAsync($"/health?search={secret}");
        using var missingResponse = await client.GetAsync($"/lists/{secret}");
        factory.Services.GetRequiredService<TracerProvider>().ForceFlush();
        factory.Services.GetRequiredService<MeterProvider>().ForceFlush();

        // Assert
        var serverSpan = activities.First(activity =>
            activity.Kind == ActivityKind.Server &&
            activity.TraceId.ToHexString() == traceId &&
            activity.DisplayName == "GET /health");
        await Assert.That(serverSpan.DisplayName).IsEqualTo("GET /health");
        await Assert.That(serverSpan.GetTagItem("http.route")).IsEqualTo("/health");
        await Assert.That(activities
            .Where(activity => activity.TraceId.ToHexString() == traceId)
            .SelectMany(activity => activity.Tags)
            .Any(tag =>
                tag.Key is "url.query" or "url.path" or "url.full" or "http.target" ||
                tag.Value?.Contains(secret) == true))
            .IsFalse();

        var requestDurations = metrics
            .Where(metric => metric.Name == "http.server.request.duration")
            .ToArray();
        var routeTags = requestDurations
            .SelectMany(metric => GetMetricTagValues(metric, "http.route"))
            .ToArray();
        await Assert.That(routeTags).Contains("/health");
        await Assert.That(requestDurations.Any(metric =>
                ContainsMetricTagValue(metric, secret)))
            .IsFalse();
    }

    [Test]
    public async Task Host_exports_identified_runtime_and_request_health_metrics()
    {
        // Arrange
        var activities = new ConcurrentCollection<Activity>();
        var metrics = new ConcurrentCollection<Metric>();
        await using var factory = CreateFactory(
            activities,
            metrics,
            includeFailureEndpoint: true);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/telemetry-failure");
        var tracerProvider = factory.Services.GetRequiredService<TracerProvider>();
        tracerProvider.ForceFlush();
        factory.Services.GetRequiredService<MeterProvider>().ForceFlush();

        // Assert
        var resourceAttributes = tracerProvider.GetResource().Attributes
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
        await Assert.That(resourceAttributes["service.name"])
            .IsEqualTo("GroceryHelper.Api");
        await Assert.That(resourceAttributes["service.version"].ToString()).IsNotEmpty();
        await Assert.That(resourceAttributes["deployment.environment.name"])
            .IsEqualTo("Testing");

        var metricNames = metrics.Select(metric => metric.Name).ToArray();
        await Assert.That(metricNames).Contains("http.server.request.duration");
        await Assert.That(metricNames).Contains("http.server.active_requests");
        await Assert.That(metricNames).Contains("dotnet.process.cpu.time");
        await Assert.That(metricNames).Contains("dotnet.process.memory.working_set");
        await Assert.That(metricNames).Contains("dotnet.gc.collections");
        await Assert.That(metricNames).Contains("dotnet.thread_pool.thread.count");

        var errorTypes = metrics
            .Where(metric => metric.Name == "http.server.request.duration")
            .SelectMany(metric => GetMetricTagValues(metric, "error.type"))
            .ToArray();
        await Assert.That(errorTypes).Contains("500");
    }

    [Test]
    public async Task Shutdown_flushes_telemetry_with_the_configured_bound()
    {
        // Arrange
        var exporter = new RecordingFailingActivityExporter();
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting(
                    "Observability:OpenTelemetry:FlushTimeoutMilliseconds",
                    "25");
                builder.ConfigureServices(services => services
                    .AddOpenTelemetry()
                    .WithTracing(tracing => tracing.AddProcessor(
                        new SimpleActivityExportProcessor(exporter))));
            });
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health");
        await factory.DisposeAsync();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(exporter.FlushTimeouts).Contains(25);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        ICollection<Activity> activities,
        ICollection<Metric> metrics,
        bool includeFailureEndpoint = false) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    services.AddOpenTelemetry()
                        .WithTracing(tracing => tracing.AddInMemoryExporter(activities))
                        .WithMetrics(metering => metering.AddInMemoryExporter(metrics));
                    if (includeFailureEndpoint)
                    {
                        services.AddSingleton<IStartupFilter, FailureStartupFilter>();
                    }
                });
            });

    private sealed class FailureStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            application =>
            {
                application.Use(async (context, nextMiddleware) =>
                {
                    if (context.Request.Path == "/telemetry-failure")
                    {
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        return;
                    }

                    await nextMiddleware();
                });
                next(application);
            };
    }

    private sealed class RecordingFailingActivityExporter : BaseExporter<Activity>
    {
        public List<int> FlushTimeouts { get; } = [];

        public override ExportResult Export(in Batch<Activity> batch) =>
            ExportResult.Failure;

        protected override bool OnForceFlush(int timeoutMilliseconds)
        {
            FlushTimeouts.Add(timeoutMilliseconds);
            return false;
        }
    }

    private static string?[] GetMetricTagValues(Metric metric, string tagName)
    {
        var values = new List<string?>();
        foreach (ref readonly var point in metric.GetMetricPoints())
        {
            foreach (var tag in point.Tags)
            {
                if (tag.Key == tagName)
                {
                    values.Add(tag.Value?.ToString());
                }
            }
        }

        return [.. values];
    }

    private static bool ContainsMetricTagValue(Metric metric, string value)
    {
        foreach (ref readonly var point in metric.GetMetricPoints())
        {
            foreach (var tag in point.Tags)
            {
                if (tag.Value?.ToString()?.Contains(value) == true)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed class ConcurrentCollection<T> : ICollection<T>
    {
        private readonly Lock sync = new();
        private readonly List<T> items = [];

        public int Count
        {
            get
            {
                lock (sync)
                {
                    return items.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        public void Add(T item)
        {
            lock (sync)
            {
                items.Add(item);
            }
        }

        public void Clear()
        {
            lock (sync)
            {
                items.Clear();
            }
        }

        public bool Contains(T item)
        {
            lock (sync)
            {
                return items.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (sync)
            {
                items.CopyTo(array, arrayIndex);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (sync)
            {
                return ((IEnumerable<T>)[.. items]).GetEnumerator();
            }
        }

        public bool Remove(T item)
        {
            lock (sync)
            {
                return items.Remove(item);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
