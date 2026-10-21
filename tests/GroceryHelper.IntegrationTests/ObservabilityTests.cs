using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GroceryHelper.IntegrationTests;

public class ObservabilityTests
{
    [Test]
    public async Task Request_is_logged_as_structured_data_without_sensitive_inputs()
    {
        // Arrange
        const string secret = "sentinel-secret-value";
        const string traceId = "0af7651916cd43dd8448eb211c80319c";
        using var temporaryDirectory = new TemporaryDirectory();
        var logPath = Path.Combine(temporaryDirectory.DirectoryPath, "api-.ndjson");
        await using (var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Observability:File:Enabled"] = "true",
            ["Observability:File:Path"] = logPath,
            ["Observability:MinimumLevel"] = "Debug",
        }))
        {
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "Authorization",
                $"Bearer {secret}");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"session={secret}");
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "traceparent",
                $"00-{traceId}-b7ad6b7169203331-01");

            // Act
            using var response = await client.GetAsync($"/health?search={secret}");
            var rolledLogPath = Directory.GetFiles(
                temporaryDirectory.DirectoryPath,
                "api-*.ndjson").Single();
            await using var logStream = new FileStream(
                rolledLogPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var logReader = new StreamReader(logStream);
            var log = await logReader.ReadToEndAsync();
            using var requestEvent = JsonDocument.Parse(
                log.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .Single(line => line.Contains("HttpRequestCompleted", StringComparison.Ordinal)));
            var properties = requestEvent.RootElement;

            // Assert
            await Assert.That(properties.GetProperty("RequestMethod").GetString()).IsEqualTo("GET");
            await Assert.That(properties.GetProperty("RouteTemplate").GetString()).IsEqualTo("/health");
            await Assert.That(properties.GetProperty("StatusCode").GetInt32()).IsEqualTo(200);
            await Assert.That(properties.GetProperty("ElapsedMilliseconds").GetDouble()).IsGreaterThanOrEqualTo(0);
            await Assert.That(properties.GetProperty("TraceId").GetString()).IsEqualTo(traceId);
            await Assert.That(log).DoesNotContain(secret);
        }
    }

    [Test]
    public async Task Invalid_file_sink_configuration_does_not_prevent_requests()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryDirectory();
        await using (var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Observability:File:Enabled"] = "true",
            ["Observability:File:Path"] = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "api-.ndjson"),
            ["Observability:File:SizeLimitBytes"] = "0",
        }))
        {
            using var client = factory.CreateClient();

            // Act
            using var response = await client.GetAsync("/health");

            // Assert
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
    }

    [Test]
    public async Task Testing_does_not_create_a_shared_log_file_by_default()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryDirectory();
        await using (var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Observability:File:Path"] = Path.Combine(
                temporaryDirectory.DirectoryPath,
                "api-.ndjson"),
        }))
        {
            using var client = factory.CreateClient();

            // Act
            using var response = await client.GetAsync("/health");

            // Assert
            await Assert.That(Directory.GetFiles(temporaryDirectory.DirectoryPath)).IsEmpty();
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IReadOnlyDictionary<string, string?> configuration) =>
        new ConfiguredWebApplicationFactory(configuration);

    private sealed class ConfiguredWebApplicationFactory(
        IReadOnlyDictionary<string, string?> configuration)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            foreach (var (key, value) in configuration)
            {
                builder.UseSetting(key, value);
            }
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                $"grocery-helper-observability-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public void Dispose()
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
