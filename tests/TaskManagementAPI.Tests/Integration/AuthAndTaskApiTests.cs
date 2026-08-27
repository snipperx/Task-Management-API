using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Domain;
using Xunit;

namespace TaskManagementAPI.Tests.Integration;

public class AuthAndTaskApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public AuthAndTaskApiTests(ApiFactory factory) => _client = factory.CreateClient();

    private async Task<string> LoginAsync(string email, string password)
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<AuthResponse>(Json);
        return body!.AccessToken;
    }

    private void Authorize(string token)
        => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task Health_endpoint_is_healthy()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_without_token_is_401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var res = await _client.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Register_then_login_issues_working_token()
    {
        var email = $"it-{Guid.NewGuid():N}@test.local";
        var reg = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email, password = "Sup3rSecret!", firstName = "Inte", lastName = "Gration"
        });
        reg.EnsureSuccessStatusCode();

        var token = await LoginAsync(email, "Sup3rSecret!");
        Authorize(token);

        var me = await _client.GetAsync("/api/tasks?pageSize=1");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Viewer_cannot_create_task()
    {
        Authorize(await LoginAsync("viewer@company.com", "Viewer@123"));

        var res = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "should fail", projectId = Guid.NewGuid(), priority = "Low"
        });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Manager_can_run_full_task_lifecycle()
    {
        Authorize(await LoginAsync("manager@company.com", "Manager@123"));

        var projects = await _client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects?status=Active", Json);
        var projectId = projects!.Items[0].Id;

        var create = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Integration lifecycle task",
            projectId,
            priority = "Medium",
            estimatedHours = 4
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var task = await create.Content.ReadFromJsonAsync<TaskDto>(Json);

        // illegal skip
        var illegal = await _client.PatchAsJsonAsync($"/api/tasks/{task!.Id}/status", new { status = "Done" });
        Assert.Equal(HttpStatusCode.Conflict, illegal.StatusCode);

        // legal walk
        foreach (var status in new[] { "InProgress", "InReview", "Done" })
        {
            var step = await _client.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", new { status });
            Assert.Equal(HttpStatusCode.OK, step.StatusCode);
        }

        var final = await _client.GetFromJsonAsync<TaskDto>($"/api/tasks/{task.Id}", Json);
        Assert.Equal(WorkItemStatus.Done, final!.Status);
        Assert.NotNull(final.CompletedAt);

        // comment
        var comment = await _client.PostAsJsonAsync($"/api/tasks/{task.Id}/comments", new { content = "done and dusted" });
        Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
    }

    [Fact]
    public async Task Past_due_date_is_rejected_with_400()
    {
        Authorize(await LoginAsync("manager@company.com", "Manager@123"));
        var projects = await _client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects?status=Active", Json);

        var res = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "past due task",
            projectId = projects!.Items[0].Id,
            priority = "Low",
            dueDate = "2020-01-01T00:00:00Z"
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
