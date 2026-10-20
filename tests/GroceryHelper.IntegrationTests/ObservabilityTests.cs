using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GroceryHelper.IntegrationTests;

public class ObservabilityTests
{
    [Test, NotInParallel]
    public async Task Request_is_logged_as_structured_data_without_sensitive_inputs()
    {
        // Arrange
        const string secret = "sentinel-secret-value";
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"grocery-helper-observability-{Guid.NewGuid():N}");
        var logPath = Path.Combine(temporaryDirectory, "api-.ndjson");
        Directory.CreateDirectory(temporaryDirectory);
        var overrides = new Dictionary<string, string?>
        {
            ["Observability__File__Enabled"] = "true",
            ["Observability__File__Path"] = logPath,
            ["Observability__MinimumLevel"] = "Debug",
        };
        var previousValues = overrides.Keys.ToDictionary(
            key => key,
            Environment.GetEnvironmentVariable);
        foreach (var (key, value) in overrides)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        try
        {
            // Act
            await using (var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseEnvironment("Testing")))
            {
                using var client = factory.CreateClient();
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Authorization",
                    $"Bearer {secret}");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", $"session={secret}");
                using var response = await client.GetAsync($"/health?search={secret}");
            }

            var rolledLogPath = Directory.GetFiles(temporaryDirectory, "api-*.ndjson").Single();
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
            await Assert.That(properties.GetProperty("TraceId").GetString()).Length().IsEqualTo(32);
            await Assert.That(log).DoesNotContain(secret);
        }
        finally
        {
            foreach (var (key, value) in previousValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test, NotInParallel]
    public async Task Invalid_file_sink_configuration_does_not_prevent_requests()
    {
        // Arrange
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"grocery-helper-observability-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var overrides = new Dictionary<string, string?>
        {
            ["Observability__File__Enabled"] = "true",
            ["Observability__File__Path"] = Path.Combine(temporaryDirectory, "api-.ndjson"),
            ["Observability__File__SizeLimitBytes"] = "0",
        };
        var previousValues = overrides.Keys.ToDictionary(
            key => key,
            Environment.GetEnvironmentVariable);
        foreach (var (key, value) in overrides)
        {
            Environment.SetEnvironmentVariable(key, value);
        }

        try
        {
            await using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
            using var client = factory.CreateClient();

            // Act
            using var response = await client.GetAsync("/health");

            // Assert
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
        finally
        {
            foreach (var (key, value) in previousValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test, NotInParallel]
    public async Task Testing_does_not_create_a_shared_log_file_by_default()
    {
        // Arrange
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"grocery-helper-observability-{Guid.NewGuid():N}");
        var logPath = Path.Combine(temporaryDirectory, "api-.ndjson");
        var previousPath = Environment.GetEnvironmentVariable("Observability__File__Path");
        Directory.CreateDirectory(temporaryDirectory);
        Environment.SetEnvironmentVariable("Observability__File__Path", logPath);

        try
        {
            await using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
            using var client = factory.CreateClient();

            // Act
            using var response = await client.GetAsync("/health");

            // Assert
            await Assert.That(Directory.GetFiles(temporaryDirectory)).IsEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("Observability__File__Path", previousPath);
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
