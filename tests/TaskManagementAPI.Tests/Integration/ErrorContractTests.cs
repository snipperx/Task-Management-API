using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using Xunit;

namespace TaskManagementAPI.Tests.Integration;

/// <summary>
/// Snapshot tests (Verify) that pin the shape of the shared <see cref="ErrorResponse"/> body.
/// Runs against EF-InMemory (no Docker). Update snapshots with `dotnet verify accept` or by
/// reviewing the `*.received.txt` files next to `*.verified.txt`.
/// </summary>
public class ErrorContractTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public ErrorContractTests(ApiFactory factory) => _client = factory.CreateClient();

    private async Task Authorize(string email, string password)
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<AuthResponse>(Json);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    [Fact]
    public async Task Validation_error_body()
    {
        await Authorize("manager@company.com", "Manager@123");

        var res = await _client.PostAsJsonAsync("/api/projects", new { name = "no" }); // too short
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<ErrorResponse>(Json);
        await Verify(body);
    }

    [Fact]
    public async Task Not_found_error_body()
    {
        await Authorize("dev1@company.com", "Dev@123");

        var res = await _client.GetAsync($"/api/tasks/{Guid.Empty}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<ErrorResponse>(Json);
        await Verify(body);
    }

    [Fact]
    public async Task Workflow_conflict_error_body()
    {
        await Authorize("manager@company.com", "Manager@123");
        var projects = await _client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects?status=Active", Json);

        var create = await _client.PostAsJsonAsync("/api/tasks", new
        {
            title = "conflict probe",
            projectId = projects!.Items[0].Id,
            priority = "Low",
        });
        var task = await create.Content.ReadFromJsonAsync<TaskDto>(Json);

        var illegal = await _client.PatchAsJsonAsync($"/api/tasks/{task!.Id}/status", new { status = "Done" });
        Assert.Equal(HttpStatusCode.Conflict, illegal.StatusCode);

        var body = await illegal.Content.ReadFromJsonAsync<ErrorResponse>(Json);
        await Verify(body);
    }
}
