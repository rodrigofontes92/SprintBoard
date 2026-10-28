using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

    private static async Task<string> GetAntiforgeryToken(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/auth/antiforgery", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return json.RootElement.GetProperty("requestToken").GetString()
            ?? throw new InvalidOperationException("The API returned an empty antiforgery token.");
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
