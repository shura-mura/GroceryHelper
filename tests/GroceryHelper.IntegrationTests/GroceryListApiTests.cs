using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GroceryHelper.IntegrationTests;

public class GroceryListApiTests
{
    [Test]
    public async Task List_reads_and_creates_require_authentication()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        using var readResponse = await client.GetAsync("/api/lists");
        using var createResponse = await client.PostAsJsonAsync(
            "/api/lists",
            new { name = "Weekly groceries" });

        // Assert
        await Assert.That(readResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Authenticated_account_creates_and_reads_its_first_list()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var login = await LoginAsync(client);
        var csrfToken = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/lists")
        {
            Content = JsonContent.Create(new { name = "  Weekly\t groceries  " }),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

        // Act
        using var createResponse = await client.SendAsync(request);
        var created = await createResponse.Content.ReadFromJsonAsync<ListResponse>();
        using var firstReadResponse = await client.GetAsync("/api/lists");
        var firstRead = await firstReadResponse.Content.ReadFromJsonAsync<ListResponse[]>();
        using var refreshReadResponse = await client.GetAsync("/api/lists");
        var refreshRead = await refreshReadResponse.Content.ReadFromJsonAsync<ListResponse[]>();

        // Assert
        await Assert.That(login.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(created).IsNotNull();
        await Assert.That(Guid.TryParse(created!.Id, out var id)).IsTrue();
        await Assert.That(id).IsNotEqualTo(Guid.Empty);
        await Assert.That(created.Name).IsEqualTo("Weekly groceries");
        await Assert.That(firstReadResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(firstRead).IsEquivalentTo([created]);
        await Assert.That(refreshReadResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(refreshRead).IsEquivalentTo([created]);
    }

    [Test]
    public async Task Authenticated_account_creates_and_reads_multiple_lists()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var login = await LoginAsync(client);
        var csrfToken = await GetCsrfTokenAsync(client);

        // Act
        using var marketResponse = await CreateListAsync(client, csrfToken, "Market");
        var market = await marketResponse.Content.ReadFromJsonAsync<ListResponse>();
        using var weeklyResponse = await CreateListAsync(client, csrfToken, "Weekly groceries");
        var weekly = await weeklyResponse.Content.ReadFromJsonAsync<ListResponse>();
        using var readResponse = await client.GetAsync("/api/lists");
        var lists = await readResponse.Content.ReadFromJsonAsync<ListResponse[]>();

        // Assert
        await Assert.That(login.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(marketResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(weeklyResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(market).IsNotNull();
        await Assert.That(weekly).IsNotNull();
        await Assert.That(market!.Id).IsNotEqualTo(weekly!.Id);
        await Assert.That(lists).IsEquivalentTo([market, weekly]);
    }

    [Test]
    public async Task Missing_or_invalid_csrf_proof_cannot_create_a_list()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var login = await LoginAsync(client);
        using var invalidRequest = new HttpRequestMessage(HttpMethod.Post, "/api/lists")
        {
            Content = JsonContent.Create(new { name = "Weekly groceries" }),
        };
        invalidRequest.Headers.Add("X-CSRF-TOKEN", "invalid-proof");

        // Act
        using var missingResponse = await client.PostAsJsonAsync(
            "/api/lists",
            new { name = "Weekly groceries" });
        using var invalidResponse = await client.SendAsync(invalidRequest);
        using var readResponse = await client.GetAsync("/api/lists");
        var lists = await readResponse.Content.ReadFromJsonAsync<ListResponse[]>();

        // Assert
        await Assert.That(login.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(missingResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(invalidResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(lists).IsEmpty();
    }

    [Test]
    [Arguments("")]
    [Arguments("Weekly\ngroceries")]
    public async Task Invalid_name_uses_validation_problem_details_with_name_error(string name)
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var login = await LoginAsync(client);
        var csrfToken = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/lists")
        {
            Content = JsonContent.Create(new { name }),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

        // Act
        using var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        await Assert.That(login.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");
        await Assert.That(problem.GetProperty("type").GetString())
            .IsEqualTo("urn:grocery-helper:problem:validation-error");
        await Assert.That(problem.GetProperty("title").GetString())
            .IsEqualTo("Validation failed");
        await Assert.That(problem.GetProperty("status").GetInt32())
            .IsEqualTo((int)HttpStatusCode.BadRequest);
        await Assert.That(problem.GetProperty("traceId").GetString()).IsNotEmpty();
        await Assert.That(problem.GetProperty("errors").GetProperty("/name").GetArrayLength())
            .IsEqualTo(1);
    }

    [Test]
    public async Task Duplicate_name_returns_validation_problem_with_existing_list_id()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var login = await LoginAsync(client);
        var csrfToken = await GetCsrfTokenAsync(client);
        using var originalResponse = await CreateListAsync(client, csrfToken, "Weekly groceries");
        var original = await originalResponse.Content.ReadFromJsonAsync<ListResponse>();

        // Act
        using var duplicateResponse = await CreateListAsync(
            client,
            csrfToken,
            "  weekly\tGROCERIES  ");
        var problem = await duplicateResponse.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        await Assert.That(login.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(originalResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(duplicateResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(duplicateResponse.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");
        await Assert.That(problem.GetProperty("type").GetString())
            .IsEqualTo("urn:grocery-helper:problem:validation-error");
        await Assert.That(problem.GetProperty("errors").GetProperty("/name")[0].GetString())
            .IsEqualTo("A grocery list named 'Weekly groceries' already exists.");
        await Assert.That(problem.GetProperty("existingListId").GetString())
            .IsEqualTo(original!.Id);
    }

    [Test]
    public async Task Concurrent_duplicate_creates_produce_exactly_one_list()
    {
        // Arrange
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { HandleCookies = true });
        using var login = await LoginAsync(client);
        var csrfToken = await GetCsrfTokenAsync(client);

        // Act
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 12)
                .Select(index => CreateListAsync(
                    client,
                    csrfToken,
                    index % 2 == 0 ? "Market" : "market")));
        var responseBodies = await Task.WhenAll(
            responses.Select(response => response.Content.ReadFromJsonAsync<JsonElement>()));
        using var readResponse = await client.GetAsync("/api/lists");
        var lists = await readResponse.Content.ReadFromJsonAsync<ListResponse[]>();

        // Assert
        await Assert.That(login.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(responses.Count(response => response.StatusCode == HttpStatusCode.Created))
            .IsEqualTo(1);
        await Assert.That(responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest))
            .IsEqualTo(11);
        await Assert.That(lists).Count().IsEqualTo(1);
        var existingId = lists![0].Id;
        var returnedIds = responses.Zip(responseBodies)
            .Select(pair => pair.First.StatusCode == HttpStatusCode.Created
                ? pair.Second.GetProperty("id").GetString()
                : pair.Second.GetProperty("existingListId").GetString());
        foreach (var returnedId in returnedIds)
        {
            await Assert.That(returnedId).IsEqualTo(existingId);
        }

        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client)
    {
        var csrfToken = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                username = "demo",
                password = "GroceryDemo1!",
            }),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> CreateListAsync(
        HttpClient client,
        string? csrfToken,
        string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/lists")
        {
            Content = JsonContent.Create(new { name }),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return await client.SendAsync(request);
    }

    private static async Task<string?> GetCsrfTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token")
            .GetString();
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

    private sealed record ListResponse(string Id, string Name);
}
