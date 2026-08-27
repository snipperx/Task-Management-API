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

public class UsersProjectsCommentsApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public UsersProjectsCommentsApiTests(ApiFactory factory) => _client = factory.CreateClient();

    private async Task<HttpClient> AsAsync(string email, string password)
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<AuthResponse>(Json);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return _client;
    }

    [Fact]
    public async Task Developer_cannot_list_users()
    {
        await AsAsync("dev1@company.com", "Dev@123");
        var res = await _client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_get_update_and_change_role()
    {
        await AsAsync("admin@company.com", "Admin@123");

        var page = await _client.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?pageSize=50", Json);
        Assert.NotNull(page);
        var dev2 = page!.Items.First(u => u.Email == "dev2@company.com");

        var byId = await _client.GetFromJsonAsync<UserDto>($"/api/users/{dev2.Id}", Json);
        Assert.Equal("dev2@company.com", byId!.Email);

        var upd = await _client.PutAsJsonAsync($"/api/users/{dev2.Id}", new
        {
            firstName = "Michael", lastName = "Renamed", isActive = true
        });
        upd.EnsureSuccessStatusCode();
        var updated = await upd.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.Equal("Michael Renamed", updated!.FullName);

        var role = await _client.PostAsJsonAsync($"/api/users/{dev2.Id}/roles", new { role = "Manager" });
        role.EnsureSuccessStatusCode();
        var roled = await role.Content.ReadFromJsonAsync<UserDto>(Json);
        Assert.Equal(UserRole.Manager, roled!.Role);
    }

    [Fact]
    public async Task Admin_can_deactivate_a_user()
    {
        var email = $"gone-{Guid.NewGuid():N}@test.local";
        var reg = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email, password = "Sup3rSecret!", firstName = "To", lastName = "Delete"
        });
        var created = await reg.Content.ReadFromJsonAsync<AuthResponse>(Json);

        await AsAsync("admin@company.com", "Admin@123");
        var del = await _client.DeleteAsync($"/api/users/{created!.User.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        _client.DefaultRequestHeaders.Authorization = null;
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Sup3rSecret!" });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_delete_own_account()
    {
        var me = await AsAsync("admin@company.com", "Admin@123");
        var page = await me.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?pageSize=50", Json);
        var adminId = page!.Items.First(u => u.Email == "admin@company.com").Id;

        var del = await _client.DeleteAsync($"/api/users/{adminId}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
    }

    [Fact]
    public async Task Manager_project_crud_and_statistics()
    {
        await AsAsync("manager@company.com", "Manager@123");

        var create = await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "Greenfield Service", description = "brand new"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var project = await create.Content.ReadFromJsonAsync<ProjectDto>(Json);
        Assert.Equal(ProjectStatus.Active, project!.Status);

        var get = await _client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}", Json);
        Assert.Equal("Greenfield Service", get!.Name);

        var upd = await _client.PutAsJsonAsync($"/api/projects/{project.Id}", new
        {
            name = "Greenfield Service v2", description = "iterated", status = "Completed"
        });
        upd.EnsureSuccessStatusCode();
        var updated = await upd.Content.ReadFromJsonAsync<ProjectDto>(Json);
        Assert.Equal(ProjectStatus.Completed, updated!.Status);
        Assert.NotNull(updated.CompletedAt);

        var stats = await _client.GetFromJsonAsync<ProjectStatisticsDto>($"/api/projects/{project.Id}/statistics", Json);
        Assert.Equal(0, stats!.TotalTasks);

        var del = await _client.DeleteAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
    }

    [Fact]
    public async Task Project_with_active_tasks_cannot_be_deleted()
    {
        await AsAsync("manager@company.com", "Manager@123");
        var projects = await _client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects?status=Active", Json);
        var withTasks = projects!.Items.First(p => p.TaskCount > 0);

        var del = await _client.DeleteAsync($"/api/projects/{withTasks.Id}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
    }

    [Fact]
    public async Task Comment_can_be_edited_and_deleted_by_author()
    {
        await AsAsync("manager@company.com", "Manager@123");
        var projects = await _client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects?status=Active", Json);
        var projectId = projects!.Items[0].Id;

        var task = await (await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Task for comment test", projectId, priority = "Low"
        })).Content.ReadFromJsonAsync<TaskDto>(Json);

        var created = await (await _client.PostAsJsonAsync($"/api/tasks/{task!.Id}/comments",
            new { content = "initial" })).Content.ReadFromJsonAsync<CommentDto>(Json);

        var edit = await _client.PutAsJsonAsync($"/api/comments/{created!.Id}", new { content = "revised" });
        edit.EnsureSuccessStatusCode();
        var edited = await edit.Content.ReadFromJsonAsync<CommentDto>(Json);
        Assert.Equal("revised", edited!.Content);
        Assert.NotNull(edited.UpdatedAt);

        var list = await _client.GetFromJsonAsync<List<CommentDto>>($"/api/tasks/{task.Id}/comments", Json);
        Assert.Single(list!);

        var del = await _client.DeleteAsync($"/api/comments/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
    }

    [Fact]
    public async Task Unknown_task_returns_404_with_error_body()
    {
        await AsAsync("dev1@company.com", "Dev@123");
        var res = await _client.GetAsync($"/api/tasks/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        var err = await res.Content.ReadFromJsonAsync<ErrorResponse>(Json);
        Assert.Equal(404, err!.Status);
        Assert.False(string.IsNullOrWhiteSpace(err.CorrelationId));
    }
}
