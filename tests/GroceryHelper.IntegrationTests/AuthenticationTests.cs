using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GroceryHelper.Api.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Net.Http.Headers;

namespace GroceryHelper.IntegrationTests;

public class AuthenticationTests
{
    [Test]
    public async Task Documented_credentials_create_an_authenticated_session()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });
        var csrfToken = await GetCsrfTokenAsync(client);
        using var loginRequest = CreateLoginRequest(
            " DEMO ",
            "GroceryDemo1!",
            csrfToken);

        // Act
        using var loginResponse = await client.SendAsync(loginRequest);
        using var sessionResponse = await client.GetAsync("/api/auth/session");
        var session = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(sessionResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(session.GetProperty("username").GetString()).IsEqualTo("demo");
    }

    [Test]
    [Arguments("other", "GroceryDemo1!")]
    [Arguments("demo", "incorrect")]
    public async Task Invalid_credentials_return_one_message_and_preserve_the_session(
        string username,
        string password)
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
        });
        using var successfulLogin = await LoginAsync(
            client,
            "demo",
            "GroceryDemo1!");

        // Act
        using var failedLogin = await LoginAsync(client, username, password);
        var failure = await failedLogin.Content.ReadFromJsonAsync<JsonElement>();
        using var sessionResponse = await client.GetAsync("/api/auth/session");

        // Assert
        await Assert.That(successfulLogin.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(failedLogin.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(failure.GetProperty("detail").GetString())
            .IsEqualTo("Invalid username or password");
        await Assert.That(sessionResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Missing_or_invalid_csrf_proof_cannot_create_or_revoke_a_session()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var authenticatedClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var anonymousClient = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var successfulLogin = await LoginAsync(
            authenticatedClient,
            "demo",
            "GroceryDemo1!");
        var loginContent = JsonContent.Create(new
        {
            username = "demo",
            password = "GroceryDemo1!",
        });

        // Act
        using var rejectedAuthenticatedLogin = await authenticatedClient.PostAsync(
            "/api/auth/login",
            loginContent);
        using var invalidProofRequest = CreateLoginRequest(
            "demo",
            "GroceryDemo1!",
            "invalid-proof");
        using var rejectedInvalidProofLogin = await authenticatedClient.SendAsync(
            invalidProofRequest);
        using var retainedSession = await authenticatedClient.GetAsync("/api/auth/session");
        using var rejectedAnonymousLogin = await anonymousClient.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "demo", password = "GroceryDemo1!" });
        using var anonymousSession = await anonymousClient.GetAsync("/api/auth/session");

        // Assert
        await Assert.That(successfulLogin.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(rejectedAuthenticatedLogin.StatusCode)
            .IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(rejectedInvalidProofLogin.StatusCode)
            .IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(retainedSession.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(rejectedAnonymousLogin.StatusCode)
            .IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(anonymousSession.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Anonymous_bootstrap_discloses_only_csrf_proof()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        using var csrfResponse = await client.GetAsync("/api/auth/csrf");
        var body = await csrfResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        using var sessionResponse = await client.GetAsync("/api/auth/session");

        // Assert
        await Assert.That(csrfResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(document.RootElement.EnumerateObject().Select(property => property.Name))
            .IsEquivalentTo(["token"]);
        await Assert.That(body).DoesNotContain("demo");
        await Assert.That(body).DoesNotContain("grocery");
        await Assert.That(sessionResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    [Arguments("http://localhost", false)]
    [Arguments("https://localhost", true)]
    public async Task Authentication_cookies_have_safe_flags_for_the_request_scheme(
        string baseAddress,
        bool secureExpected)
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(baseAddress),
            HandleCookies = true,
        });

        // Act
        using var csrfResponse = await client.GetAsync("/api/auth/csrf");
        var csrfToken = await ReadCsrfTokenAsync(csrfResponse);
        using var request = CreateLoginRequest("demo", "GroceryDemo1!", csrfToken);
        using var loginResponse = await client.SendAsync(request);
        var csrfCookie = csrfResponse.Headers.GetValues("Set-Cookie").Single();
        var sessionCookie = loginResponse.Headers.GetValues("Set-Cookie").Single();

        // Assert
        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(csrfCookie).Contains("httponly", StringComparison.OrdinalIgnoreCase);
        await Assert.That(csrfCookie).Contains("samesite=strict", StringComparison.OrdinalIgnoreCase);
        await Assert.That(sessionCookie).Contains("httponly", StringComparison.OrdinalIgnoreCase);
        await Assert.That(sessionCookie).Contains("samesite=lax", StringComparison.OrdinalIgnoreCase);
        await Assert.That(csrfCookie.Contains("secure", StringComparison.OrdinalIgnoreCase))
            .IsEqualTo(secureExpected);
        await Assert.That(sessionCookie.Contains("secure", StringComparison.OrdinalIgnoreCase))
            .IsEqualTo(secureExpected);
    }

    [Test]
    public async Task Signing_in_again_keeps_both_sessions_authenticated()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var firstBrowser = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var secondBrowser = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var firstLogin = await LoginAsync(firstBrowser, "demo", "GroceryDemo1!");

        // Act
        using var secondLogin = await LoginAsync(secondBrowser, "demo", "GroceryDemo1!");
        using var firstSession = await firstBrowser.GetAsync("/api/auth/session");
        using var secondSession = await secondBrowser.GetAsync("/api/auth/session");

        // Assert
        await Assert.That(firstLogin.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(secondLogin.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(firstSession.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(secondSession.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Session_and_cookie_expire_at_the_original_eight_hour_deadline()
    {
        // Arrange
        var loginTime = new DateTimeOffset(2035, 4, 5, 6, 7, 8, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(loginTime);
        await using var factory = CreateFactory(timeProvider);
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var loginResponse = await LoginAsync(client, "demo", "GroceryDemo1!");
        var sessionCookie = SetCookieHeaderValue.Parse(
            loginResponse.Headers.GetValues("Set-Cookie").Single());

        // Act
        timeProvider.UtcNow = loginTime.AddHours(4);
        using var activity = await client.GetAsync("/api/auth/session");
        timeProvider.UtcNow = loginTime.Add(SessionStore.Lifetime).AddTicks(-1);
        using var immediatelyBeforeExpiry = await client.GetAsync("/api/auth/session");
        timeProvider.UtcNow = loginTime.Add(SessionStore.Lifetime);
        using var atExpiry = await client.GetAsync("/api/auth/session");
        timeProvider.UtcNow = loginTime.Add(SessionStore.Lifetime).AddTicks(1);
        using var afterExpiry = await client.GetAsync("/api/auth/session");

        // Assert
        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(sessionCookie.Expires).IsEqualTo(
            loginTime.Add(SessionStore.Lifetime));
        await Assert.That(activity.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(immediatelyBeforeExpiry.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(atExpiry.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(afterExpiry.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Concurrent_sessions_keep_their_own_login_deadlines()
    {
        // Arrange
        var firstLoginTime = new DateTimeOffset(2035, 4, 5, 6, 7, 8, TimeSpan.Zero);
        var timeProvider = new MutableTimeProvider(firstLoginTime);
        await using var factory = CreateFactory(timeProvider);
        using var firstBrowser = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var secondBrowser = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var firstLogin = await LoginAsync(firstBrowser, "demo", "GroceryDemo1!");
        timeProvider.UtcNow = firstLoginTime.AddHours(1);
        using var secondLogin = await LoginAsync(secondBrowser, "demo", "GroceryDemo1!");

        // Act
        timeProvider.UtcNow = firstLoginTime.Add(SessionStore.Lifetime);
        using var firstSession = await firstBrowser.GetAsync("/api/auth/session");
        using var secondSession = await secondBrowser.GetAsync("/api/auth/session");

        // Assert
        await Assert.That(firstLogin.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(secondLogin.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(firstSession.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(secondSession.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task A_new_backend_rejects_a_session_from_the_previous_backend()
    {
        // Arrange
        string sessionCookie;
        await using (var originalFactory = CreateFactory())
        {
            using var originalClient = originalFactory.CreateClient(
                new WebApplicationFactoryClientOptions { HandleCookies = true });
            using var loginResponse = await LoginAsync(
                originalClient,
                "demo",
                "GroceryDemo1!");
            sessionCookie = loginResponse.Headers.GetValues("Set-Cookie")
                .Single()
                .Split(';', 2)[0];
        }

        await using var restartedFactory = CreateFactory();
        using var restartedClient = restartedFactory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var sessionRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        sessionRequest.Headers.Add("Cookie", sessionCookie);

        // Act
        using var sessionResponse = await restartedClient.SendAsync(sessionRequest);

        // Assert
        await Assert.That(sessionResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string username,
        string password)
    {
        var csrfToken = await GetCsrfTokenAsync(client);
        using var request = CreateLoginRequest(username, password, csrfToken);
        return await client.SendAsync(request);
    }

    private static async Task<string?> GetCsrfTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        return await ReadCsrfTokenAsync(response);
    }

    private static async Task<string?> ReadCsrfTokenAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>())
        .GetProperty("token")
        .GetString();

    private static HttpRequestMessage CreateLoginRequest(
        string username,
        string password,
        string? csrfToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { username, password }),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return request;
    }

    private static WebApplicationFactory<Program> CreateFactory(
        TimeProvider? timeProvider = null) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                if (timeProvider is not null)
                {
                    builder.ConfigureServices(services =>
                    {
                        services.RemoveAll<TimeProvider>();
                        services.AddSingleton(timeProvider);
                    });
                }
            });

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
