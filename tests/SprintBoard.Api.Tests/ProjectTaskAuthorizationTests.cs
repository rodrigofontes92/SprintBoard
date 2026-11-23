using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SprintBoard.Api.Tests;

public sealed class ProjectTaskAuthorizationTests(SprintBoardApiFactory factory)
    : IClassFixture<SprintBoardApiFactory>
{
    [Fact]
    public async Task OwnerCanCreateReadUpdateAndDeleteTasks()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateApiClient();
        await Register(client, cancellationToken);
        var projectId = await CreateProject(client, cancellationToken);

        var createToken = await GetAntiforgeryToken(client, cancellationToken);
        using var createResponse = await SendWithToken(
            client,
            HttpMethod.Post,
            $"/api/projects/{projectId}/tasks",
            createToken,
            new
            {
                title = "Draft task API",
                description = "Initial task description",
                priority = "high",
                dueDate = "2026-12-02"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(cancellationToken));
        var created = createdJson.RootElement;
        var taskId = created.GetProperty("id").GetGuid();
        Assert.Equal(projectId, created.GetProperty("projectId").GetGuid());
        Assert.Equal("Draft task API", created.GetProperty("title").GetString());
        Assert.Equal("todo", created.GetProperty("status").GetString());
        Assert.Equal("high", created.GetProperty("priority").GetString());
        Assert.Equal("2026-12-02", created.GetProperty("dueDate").GetString());

        using var listResponse = await client.GetAsync($"/api/projects/{projectId}/tasks", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listJson = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Single(listJson.RootElement.EnumerateArray());

        using var readResponse = await client.GetAsync(
            $"/api/projects/{projectId}/tasks/{taskId}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);

        var startToken = await GetAntiforgeryToken(client, cancellationToken);
        using var startResponse = await SendWithToken(
            client,
            HttpMethod.Put,
            $"/api/projects/{projectId}/tasks/{taskId}",
            startToken,
            new
            {
                title = "Implement task API",
                description = "Updated task description",
                status = "inProgress",
                priority = "medium",
                dueDate = "2026-12-10"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);

        var completeToken = await GetAntiforgeryToken(client, cancellationToken);
        using var completeResponse = await SendWithToken(
            client,
            HttpMethod.Put,
            $"/api/projects/{projectId}/tasks/{taskId}",
            completeToken,
            new
            {
                title = "Implement task API",
                description = "Updated task description",
                status = "done",
                priority = "medium",
                dueDate = "2026-12-10"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        using var persistedResponse = await client.GetAsync(
            $"/api/projects/{projectId}/tasks/{taskId}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, persistedResponse.StatusCode);
        using var persistedJson = JsonDocument.Parse(
            await persistedResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("done", persistedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("medium", persistedJson.RootElement.GetProperty("priority").GetString());
        Assert.Equal("2026-12-10", persistedJson.RootElement.GetProperty("dueDate").GetString());

        var deleteToken = await GetAntiforgeryToken(client, cancellationToken);
        using var deleteResponse = await SendWithToken(
            client,
            HttpMethod.Delete,
            $"/api/projects/{projectId}/tasks/{taskId}",
            deleteToken,
            new { },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var missingResponse = await client.GetAsync(
            $"/api/projects/{projectId}/tasks/{taskId}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    [Fact]
    public async Task InvalidTaskValuesAndStatusTransitionsAreRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateApiClient();
        await Register(client, cancellationToken);
        var projectId = await CreateProject(client, cancellationToken);

        var invalidPriorityToken = await GetAntiforgeryToken(client, cancellationToken);
        using var invalidPriorityResponse = await SendWithToken(
            client,
            HttpMethod.Post,
            $"/api/projects/{projectId}/tasks",
            invalidPriorityToken,
            new { title = "Invalid priority", description = "", priority = "critical", dueDate = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPriorityResponse.StatusCode);

        var blankTitleToken = await GetAntiforgeryToken(client, cancellationToken);
        using var blankTitleResponse = await SendWithToken(
            client,
            HttpMethod.Post,
            $"/api/projects/{projectId}/tasks",
            blankTitleToken,
            new { title = "   ", description = "", priority = "low", dueDate = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, blankTitleResponse.StatusCode);

        var createToken = await GetAntiforgeryToken(client, cancellationToken);
        using var createResponse = await SendWithToken(
            client,
            HttpMethod.Post,
            $"/api/projects/{projectId}/tasks",
            createToken,
            new { title = "Valid task", description = "", priority = "low", dueDate = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var createdJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(cancellationToken));
        var taskId = createdJson.RootElement.GetProperty("id").GetGuid();

        using var invalidTransitionResponse = await UpdateStatus(
            client,
            projectId,
            taskId,
            "done",
            cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidTransitionResponse.StatusCode);

        using var startResponse = await UpdateStatus(client, projectId, taskId, "inProgress", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        using var returnToTodoResponse = await UpdateStatus(client, projectId, taskId, "todo", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, returnToTodoResponse.StatusCode);
        using var restartResponse = await UpdateStatus(client, projectId, taskId, "inProgress", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, restartResponse.StatusCode);
        using var finishResponse = await UpdateStatus(client, projectId, taskId, "done", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, finishResponse.StatusCode);

        using var invalidReopenResponse = await UpdateStatus(client, projectId, taskId, "todo", cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, invalidReopenResponse.StatusCode);

        using var persistedResponse = await client.GetAsync(
            $"/api/projects/{projectId}/tasks/{taskId}",
            cancellationToken);
        using var persistedJson = JsonDocument.Parse(
            await persistedResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("done", persistedJson.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task TasksCannotBeReadOrChangedAcrossProjectOwnershipBoundaries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var ownerClient = factory.CreateApiClient();
        await Register(ownerClient, cancellationToken);
        var ownerProjectId = await CreateProject(ownerClient, cancellationToken);
        var ownerTaskId = await CreateTask(ownerClient, ownerProjectId, cancellationToken);

        using var otherClient = factory.CreateApiClient();
        await Register(otherClient, cancellationToken);
        var otherProjectId = await CreateProject(otherClient, cancellationToken);

        using var listResponse = await otherClient.GetAsync(
            $"/api/projects/{ownerProjectId}/tasks",
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, listResponse.StatusCode);

        using var readResponse = await otherClient.GetAsync(
            $"/api/projects/{ownerProjectId}/tasks/{ownerTaskId}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);

        var createToken = await GetAntiforgeryToken(otherClient, cancellationToken);
        using var createResponse = await SendWithToken(
            otherClient,
            HttpMethod.Post,
            $"/api/projects/{ownerProjectId}/tasks",
            createToken,
            new { title = "Unauthorized task", description = "", priority = "high", dueDate = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, createResponse.StatusCode);

        using var updateResponse = await UpdateTask(
            otherClient,
            otherProjectId,
            ownerTaskId,
            "inProgress",
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);

        var deleteToken = await GetAntiforgeryToken(otherClient, cancellationToken);
        using var deleteResponse = await SendWithToken(
            otherClient,
            HttpMethod.Delete,
            $"/api/projects/{otherProjectId}/tasks/{ownerTaskId}",
            deleteToken,
            new { },
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        using var ownerReadResponse = await ownerClient.GetAsync(
            $"/api/projects/{ownerProjectId}/tasks/{ownerTaskId}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, ownerReadResponse.StatusCode);
        using var ownerJson = JsonDocument.Parse(await ownerReadResponse.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("Test task", ownerJson.RootElement.GetProperty("title").GetString());
    }

    private static async Task Register(HttpClient client, CancellationToken cancellationToken)
    {
        var token = await GetAntiforgeryToken(client, cancellationToken);
        using var response = await SendWithToken(
            client,
            HttpMethod.Post,
            "/api/auth/register",
            token,
            new
            {
                email = $"task-{Guid.NewGuid():N}@example.test",
                password = "Strong-Password-123!"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<Guid> CreateProject(HttpClient client, CancellationToken cancellationToken)
    {
        var token = await GetAntiforgeryToken(client, cancellationToken);
        using var response = await SendWithToken(
            client,
            HttpMethod.Post,
            "/api/projects",
            token,
            new { name = $"Task test project {Guid.NewGuid():N}", description = "Project for task tests" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTask(
        HttpClient client,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var token = await GetAntiforgeryToken(client, cancellationToken);
        using var response = await SendWithToken(
            client,
            HttpMethod.Post,
            $"/api/projects/{projectId}/tasks",
            token,
            new { title = "Test task", description = "Task for authorization tests", priority = "medium", dueDate = (string?)null },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> UpdateStatus(
        HttpClient client,
        Guid projectId,
        Guid taskId,
        string status,
        CancellationToken cancellationToken) =>
        await UpdateTask(client, projectId, taskId, status, cancellationToken);

    private static async Task<HttpResponseMessage> UpdateTask(
        HttpClient client,
        Guid projectId,
        Guid taskId,
        string status,
        CancellationToken cancellationToken)
    {
        var token = await GetAntiforgeryToken(client, cancellationToken);
        return await SendWithToken(
            client,
            HttpMethod.Put,
            $"/api/projects/{projectId}/tasks/{taskId}",
            token,
            new
            {
                title = "Test task",
                description = "Task for authorization tests",
                status,
                priority = "medium",
                dueDate = (string?)null
            },
            cancellationToken);
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
