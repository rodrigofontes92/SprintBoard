using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SprintBoard.Api.Data;
using Xunit;

namespace SprintBoard.Api.Tests;

public sealed class AuthenticationAndAuthorizationTests(SprintBoardApiFactory factory)
    : IClassFixture<SprintBoardApiFactory>
{
    [Fact]
    public async Task RegistrationRequiresAnAntiforgeryToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateApiClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "missing-token@example.test", password = "Strong-Password-123!" },
            cancellationToken: cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProjectReadsAreLimitedToTheSignedInOwner()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var ownerClient = factory.CreateApiClient();
        var ownerToken = await GetAntiforgeryToken(ownerClient, cancellationToken);
        using var registrationResponse = await SendWithToken(
            ownerClient,
            HttpMethod.Post,
            "/api/auth/register",
            ownerToken,
            new { email = "owner@example.test", password = "Strong-Password-123!" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);

        ownerToken = await GetAntiforgeryToken(ownerClient, cancellationToken);
        using var createResponse = await SendWithToken(
            ownerClient,
            HttpMethod.Post,
            "/api/projects",
            ownerToken,
            new { name = "Private project", description = "Owned by the first user." },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var projectJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(cancellationToken));
        var projectId = projectJson.RootElement.GetProperty("id").GetGuid();

        using var otherUserClient = factory.CreateApiClient();
        var otherUserToken = await GetAntiforgeryToken(otherUserClient, cancellationToken);
        using var otherRegistrationResponse = await SendWithToken(
            otherUserClient,
            HttpMethod.Post,
            "/api/auth/register",
            otherUserToken,
            new { email = "other-user@example.test", password = "Strong-Password-123!" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, otherRegistrationResponse.StatusCode);

        using var listResponse = await otherUserClient.GetAsync("/api/projects", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var projectList = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Empty(projectList.RootElement.EnumerateArray());

        using var readResponse = await otherUserClient.GetAsync($"/api/projects/{projectId}", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);
    }

    [Fact]
    public async Task ProjectOwnerCanCreateReadUpdateAndDeleteProjects()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateApiClient();
        await Register(client, "crud-owner@example.test", cancellationToken);

        var createToken = await GetAntiforgeryToken(client, cancellationToken);
        using var createResponse = await SendWithToken(
            client,
            HttpMethod.Post,
            "/api/projects",
            createToken,
            new { name = "  Release plan  ", description = "  First draft  " },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(cancellationToken));
        var created = createdJson.RootElement;
        var projectId = created.GetProperty("id").GetGuid();
        Assert.Equal("Release plan", created.GetProperty("name").GetString());
        Assert.Equal("First draft", created.GetProperty("description").GetString());
        var createdAt = created.GetProperty("createdAt").GetDateTimeOffset();
        var originalUpdatedAt = created.GetProperty("updatedAt").GetDateTimeOffset();
        Assert.Equal(createdAt, originalUpdatedAt);

        using var listResponse = await client.GetAsync("/api/projects", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Single(listJson.RootElement.EnumerateArray());

        var updateToken = await GetAntiforgeryToken(client, cancellationToken);
        using var updateResponse = await SendWithToken(
            client,
            HttpMethod.Put,
            $"/api/projects/{projectId}",
            updateToken,
            new { name = "Release plan 2", description = "Updated scope" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        using var updatedJson = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("Release plan 2", updatedJson.RootElement.GetProperty("name").GetString());
        Assert.Equal("Updated scope", updatedJson.RootElement.GetProperty("description").GetString());
        Assert.Equal(createdAt, updatedJson.RootElement.GetProperty("createdAt").GetDateTimeOffset());
        Assert.True(updatedJson.RootElement.GetProperty("updatedAt").GetDateTimeOffset() > originalUpdatedAt);

        using var readResponse = await client.GetAsync($"/api/projects/{projectId}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        using var readJson = JsonDocument.Parse(await readResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("Release plan 2", readJson.RootElement.GetProperty("name").GetString());

        var deleteToken = await GetAntiforgeryToken(client, cancellationToken);
        using var deleteResponse = await SendWithToken(
            client,
            HttpMethod.Delete,
            $"/api/projects/{projectId}",
            deleteToken,
            new { },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var missingResponse = await client.GetAsync($"/api/projects/{projectId}", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    [Fact]
    public async Task ProjectOwnerCannotEditOrDeleteAnotherUsersProject()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var ownerClient = factory.CreateApiClient();
        await Register(ownerClient, "authorization-owner@example.test", cancellationToken);

        var ownerToken = await GetAntiforgeryToken(ownerClient, cancellationToken);
        using var createResponse = await SendWithToken(
            ownerClient,
            HttpMethod.Post,
            "/api/projects",
            ownerToken,
            new { name = "Owner only", description = "Original description" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var createdJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(cancellationToken));
        var projectId = createdJson.RootElement.GetProperty("id").GetGuid();

        using var otherClient = factory.CreateApiClient();
        await Register(otherClient, "authorization-other@example.test", cancellationToken);
        var otherToken = await GetAntiforgeryToken(otherClient, cancellationToken);

        using var updateResponse = await SendWithToken(
            otherClient,
            HttpMethod.Put,
            $"/api/projects/{projectId}",
            otherToken,
            new { name = "Stolen", description = "Should not persist" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);

        using var deleteResponse = await SendWithToken(
            otherClient,
            HttpMethod.Delete,
            $"/api/projects/{projectId}",
            otherToken,
            new { },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        using var ownerReadResponse = await ownerClient.GetAsync($"/api/projects/{projectId}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, ownerReadResponse.StatusCode);
        using var ownerJson = JsonDocument.Parse(await ownerReadResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("Owner only", ownerJson.RootElement.GetProperty("name").GetString());
        Assert.Equal("Original description", ownerJson.RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public async Task DuplicateProjectNamesAreRejectedForTheSameOwner()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateApiClient();
        await Register(client, "duplicate-owner@example.test", cancellationToken);

        var firstToken = await GetAntiforgeryToken(client, cancellationToken);
        using var firstResponse = await SendWithToken(
            client,
            HttpMethod.Post,
            "/api/projects",
            firstToken,
            new { name = "Same name", description = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondToken = await GetAntiforgeryToken(client, cancellationToken);
        using var secondResponse = await SendWithToken(
            client,
            HttpMethod.Post,
            "/api/projects",
            secondToken,
            new { name = "Same name", description = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task DemoSeedCanBeAppliedMoreThanOnce()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateApiClient();
        var email = $"demo-{Guid.NewGuid():N}@example.test";
        const string password = "Demo-Password-123!";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DemoSeed:Enabled"] = "true",
                ["DemoSeed:Email"] = email,
                ["DemoSeed:Password"] = password
            })
            .Build();

        await DemoDataSeeder.SeedAsync(factory.Services, configuration, cancellationToken);
        await DemoDataSeeder.SeedAsync(factory.Services, configuration, cancellationToken);

        var token = await GetAntiforgeryToken(client, cancellationToken);
        using var loginResponse = await SendWithToken(
            client,
            HttpMethod.Post,
            "/api/auth/login",
            token,
            new { email, password },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var projectsResponse = await client.GetAsync("/api/projects", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, projectsResponse.StatusCode);
        using var projectsJson = JsonDocument.Parse(await projectsResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(2, projectsJson.RootElement.GetArrayLength());
    }

    private static async Task<string> GetAntiforgeryToken(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/auth/antiforgery", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return json.RootElement.GetProperty("requestToken").GetString()
            ?? throw new InvalidOperationException("The API returned an empty antiforgery token.");
    }

    private static async Task Register(HttpClient client, string email, CancellationToken cancellationToken)
    {
        var token = await GetAntiforgeryToken(client, cancellationToken);
        using var response = await SendWithToken(
            client,
            HttpMethod.Post,
            "/api/auth/register",
            token,
            new { email, password = "Strong-Password-123!" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendWithToken(
        HttpClient client,
        HttpMethod method,
        string url,
        string token,
        object body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-CSRF-TOKEN", token);
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request, cancellationToken);
    }
}
