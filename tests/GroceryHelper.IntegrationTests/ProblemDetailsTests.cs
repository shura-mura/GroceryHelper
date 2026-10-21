using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GroceryHelper.IntegrationTests;

public class ProblemDetailsTests
{
    [Test]
    public async Task Missing_api_route_returns_correlated_problem_details()
    {
        // Arrange
        const string traceId = "0af7651916cd43dd8448eb211c80319c";
        const string secret = "sentinel-secret-value";
        await using var factory = CreateDefaultFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "traceparent",
            $"00-{traceId}-b7ad6b7169203331-01");

        // Act
        using var response = await client.GetAsync($"/api/lists?search={secret}");
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");
        await Assert.That(problem.RootElement.GetProperty("type").GetString())
            .IsEqualTo("about:blank");
        await Assert.That(problem.RootElement.GetProperty("title").GetString())
            .IsEqualTo("Not Found");
        await Assert.That(problem.RootElement.GetProperty("status").GetInt32())
            .IsEqualTo(StatusCodes.Status404NotFound);
        await Assert.That(problem.RootElement.GetProperty("traceId").GetString())
            .IsEqualTo(traceId);
        await Assert.That(problem.RootElement.TryGetProperty("instance", out _)).IsFalse();
        await Assert.That(body).DoesNotContain(secret);
    }

    [Test]
    public async Task Api_error_uses_problem_json_for_an_unaccepted_media_type()
    {
        // Arrange
        await using var factory = CreateDefaultFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/plain");

        // Act
        using var response = await client.GetAsync("/api/missing");
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");
        await Assert.That(problem.RootElement.GetProperty("status").GetInt32())
            .IsEqualTo(StatusCodes.Status404NotFound);
    }

    [Test]
    public async Task Head_api_error_remains_bodyless()
    {
        // Arrange
        await using var factory = CreateDefaultFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Head, "/api/missing");

        // Act
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(body).IsEmpty();
    }

    [Test]
    [Arguments("Testing")]
    [Arguments("Development")]
    [Arguments("Production")]
    public async Task Unhandled_api_exception_returns_generic_problem_details(
        string environment)
    {
        // Arrange
        const string exceptionMessage = "private exception detail";
        await using var factory = CreateFactory(environment, exceptionMessage);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/failure");
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);

        // Assert
        await Assert.That(response.StatusCode)
            .IsEqualTo(HttpStatusCode.InternalServerError);
        await Assert.That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");
        await Assert.That(problem.RootElement.GetProperty("type").GetString())
            .IsEqualTo("about:blank");
        await Assert.That(problem.RootElement.GetProperty("title").GetString())
            .IsEqualTo("Internal Server Error");
        await Assert.That(problem.RootElement.GetProperty("status").GetInt32())
            .IsEqualTo(StatusCodes.Status500InternalServerError);
        await Assert.That(problem.RootElement.GetProperty("traceId").GetString())
            .IsNotEmpty();
        await Assert.That(body).DoesNotContain(exceptionMessage);
    }

    [Test]
    public async Task Validation_failure_returns_structured_application_problem()
    {
        // Arrange
        const string traceId = "0af7651916cd43dd8448eb211c80319c";
        await using var factory = CreateFactory("Testing", "unused exception detail");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "traceparent",
            $"00-{traceId}-b7ad6b7169203331-01");

        // Act
        using var response = await client.PostAsync("/api/validation", content: null);
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");
        await Assert.That(problem.RootElement.GetProperty("type").GetString())
            .IsEqualTo("urn:grocery-helper:problem:validation-error");
        await Assert.That(problem.RootElement.GetProperty("title").GetString())
            .IsEqualTo("Validation failed");
        await Assert.That(problem.RootElement.GetProperty("status").GetInt32())
            .IsEqualTo(StatusCodes.Status400BadRequest);
        await Assert.That(problem.RootElement.GetProperty("traceId").GetString())
            .IsEqualTo(traceId);
        await Assert.That(
                problem.RootElement.GetProperty("errors").GetProperty("/name")[0].GetString())
            .IsEqualTo("A name is required.");
    }

    [Test]
    public async Task Status_failures_preserve_required_http_headers()
    {
        // Arrange
        await using var factory = CreateFactory("Testing", "unused exception detail");
        using var client = factory.CreateClient();
        var cases = new[]
        {
            new HeaderCase(
                "/api/auth-failure",
                HttpStatusCode.Unauthorized,
                "WWW-Authenticate",
                "Bearer realm=\"GroceryHelper\""),
            new HeaderCase(
                "/api/method-failure",
                HttpStatusCode.MethodNotAllowed,
                "Allow",
                "GET"),
            new HeaderCase(
                "/api/rate-limit",
                HttpStatusCode.TooManyRequests,
                "Retry-After",
                "60"),
        };

        // Act
        var responses = new List<(HeaderCase Case, HttpResponseMessage Response, string Body)>();
        foreach (var testCase in cases)
        {
            var response = await client.GetAsync(testCase.Path);
            responses.Add((testCase, response, await response.Content.ReadAsStringAsync()));
        }

        // Assert
        foreach (var (testCase, response, body) in responses)
        {
            using (response)
            using (var problem = JsonDocument.Parse(body))
            {
                await Assert.That(response.StatusCode).IsEqualTo(testCase.StatusCode);
                await Assert.That(response.Content.Headers.ContentType?.MediaType)
                    .IsEqualTo("application/problem+json");
                await Assert.That(GetHeaderValues(response, testCase.HeaderName))
                    .Contains(testCase.HeaderValue);
                await Assert.That(problem.RootElement.GetProperty("type").GetString())
                    .IsEqualTo("about:blank");
                await Assert.That(problem.RootElement.GetProperty("status").GetInt32())
                    .IsEqualTo((int)testCase.StatusCode);
                await Assert.That(problem.RootElement.GetProperty("traceId").GetString())
                    .IsNotEmpty();
                await Assert.That(problem.RootElement.TryGetProperty("detail", out _)).IsFalse();
            }
        }
    }

    [Test]
    public async Task Body_bearing_api_error_is_replaced_with_safe_problem_details()
    {
        // Arrange
        const string unsafeBody = "sentinel-private-response-body";
        await using var factory = CreateFactory("Testing", unsafeBody);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/body-failure");
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);

        // Assert
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");
        await Assert.That(problem.RootElement.GetProperty("type").GetString())
            .IsEqualTo("about:blank");
        await Assert.That(problem.RootElement.GetProperty("status").GetInt32())
            .IsEqualTo(StatusCodes.Status400BadRequest);
        await Assert.That(body).DoesNotContain(unsafeBody);
    }

    [Test]
    public async Task Unhandled_api_exception_is_logged_once_with_one_completion_summary()
    {
        // Arrange
        const string exceptionMessage = "sentinel-private-input";
        using var temporaryDirectory = new TemporaryDirectory();
        var logPath = Path.Combine(temporaryDirectory.DirectoryPath, "api-.ndjson");
        await using var factory = CreateFactory(
            "Testing",
            exceptionMessage,
            new Dictionary<string, string?>
            {
                ["Observability:File:Enabled"] = "true",
                ["Observability:File:Path"] = logPath,
            });
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/api/failure");
        var rolledLogPath = Directory.GetFiles(
            temporaryDirectory.DirectoryPath,
            "api-*.ndjson").Single();
        await using var logStream = new FileStream(
            rolledLogPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var logReader = new StreamReader(logStream);
        var logLines = (await logReader.ReadToEndAsync())
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var exceptionEvent = logLines.Single(line => line.Contains(
            "UnhandledApiException",
            StringComparison.Ordinal));
        var completionEvent = logLines.Single(line => line.Contains(
            "HttpRequestCompleted",
            StringComparison.Ordinal));
        using var exceptionJson = JsonDocument.Parse(exceptionEvent);
        using var completionJson = JsonDocument.Parse(completionEvent);

        // Assert
        await Assert.That(response.StatusCode)
            .IsEqualTo(HttpStatusCode.InternalServerError);
        await Assert.That(string.Join(Environment.NewLine, logLines))
            .DoesNotContain(exceptionMessage);
        await Assert.That(exceptionJson.RootElement.GetProperty("ExceptionType").GetString())
            .IsEqualTo(typeof(InvalidOperationException).FullName);
        await Assert.That(
                exceptionJson.RootElement.GetProperty("ExceptionStackTrace").GetString())
            .Contains(nameof(TestFailureStartupFilter));
        await Assert.That(exceptionJson.RootElement.GetProperty("ExceptionChain").GetString())
            .Contains(typeof(ArgumentException).FullName!);
        await Assert.That(exceptionJson.RootElement.GetProperty("TraceId").GetString())
            .IsEqualTo(completionJson.RootElement.GetProperty("TraceId").GetString());
        await Assert.That(completionJson.RootElement.GetProperty("StatusCode").GetInt32())
            .IsEqualTo(StatusCodes.Status500InternalServerError);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string environment,
        string exceptionMessage,
        IReadOnlyDictionary<string, string?>? configuration = null) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.UseSetting("Observability:File:Enabled", "false");
                builder.UseSetting(
                    "Observability:OpenTelemetry:ConsoleExporter:Enabled",
                    "false");
                foreach (var (key, value) in configuration ??
                    new Dictionary<string, string?>())
                {
                    builder.UseSetting(key, value);
                }

                builder.ConfigureServices(services =>
                    services.AddSingleton<IStartupFilter>(
                        new TestFailureStartupFilter(exceptionMessage)));
            });

    private static WebApplicationFactory<Program> CreateDefaultFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

    private static string[] GetHeaderValues(
        HttpResponseMessage response,
        string headerName)
    {
        if (response.Headers.NonValidated.TryGetValues(headerName, out var responseValues))
        {
            return [.. responseValues];
        }

        return response.Content.Headers.NonValidated.TryGetValues(
            headerName,
            out var contentValues)
            ? [.. contentValues]
            : [];
    }

    private sealed class TestFailureStartupFilter(string exceptionMessage)
        : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            application =>
            {
                next(application);
                application.Use(async (context, nextMiddleware) =>
                {
                    if (context.Request.Path == "/api/failure")
                    {
                        throw new InvalidOperationException(
                            exceptionMessage,
                            new ArgumentException(exceptionMessage));
                    }

                    if (context.Request.Path == "/api/validation")
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        var problemDetails = new HttpValidationProblemDetails(
                            new Dictionary<string, string[]>
                            {
                                ["/name"] = ["A name is required."],
                            });
                        await context.RequestServices
                            .GetRequiredService<IProblemDetailsService>()
                            .WriteAsync(new ProblemDetailsContext
                            {
                                HttpContext = context,
                                ProblemDetails = problemDetails,
                            });
                        return;
                    }

                    if (context.Request.Path == "/api/auth-failure")
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.Headers.WWWAuthenticate = "Bearer realm=\"GroceryHelper\"";
                        return;
                    }

                    if (context.Request.Path == "/api/method-failure")
                    {
                        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                        context.Response.Headers.Allow = "GET";
                        return;
                    }

                    if (context.Request.Path == "/api/rate-limit")
                    {
                        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                        context.Response.Headers.RetryAfter = "60";
                        return;
                    }

                    if (context.Request.Path == "/api/body-failure")
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        context.Response.ContentType = "text/plain";
                        await context.Response.WriteAsync(exceptionMessage);
                        return;
                    }

                    await nextMiddleware();
                });
            };
    }

    private sealed record HeaderCase(
        string Path,
        HttpStatusCode StatusCode,
        string HeaderName,
        string HeaderValue);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                $"grocery-helper-problems-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public void Dispose() => Directory.Delete(DirectoryPath, recursive: true);
    }
}
