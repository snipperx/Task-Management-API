using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bogus;
using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Domain;
using Xunit;

namespace TaskManagementAPI.Tests.Integration;

/// <summary>
/// Integration tests against a real PostgreSQL container. Demonstrates the tooling:
/// Testcontainers for the DB, Respawn for isolation (<see cref="PostgresApiFactory.ResetAsync"/>),
/// and Bogus for request payloads.
/// </summary>
[Collection(PostgresCollection.Name)]
public class RealDatabaseApiTests : IAsyncLifetime
{
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public RealDatabaseApiTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // Clean + reseed before every test in this class.
    public Task InitializeAsync() => _factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task Authorize(string email, string password)
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<AuthResponse>(Json);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    [Fact]
    public async Task Seed_data_is_present_after_reset()
    {
        await Authorize("admin@company.com", "Admin@123");

        var users = await _client.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?pageSize=50", Json);
        Assert.Equal(5, users!.TotalCount);

        var projects = await _client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects?pageSize=50", Json);
        Assert.Equal(3, projects!.TotalCount);
    }

    [Fact]
    public async Task Register_and_create_tasks_with_generated_data()
    {
        var faker = new Faker { Random = new Randomizer(4242) };

        var email = faker.Internet.ExampleEmail();
        var password = faker.Internet.Password(12) + "aA1!";
        var register = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password,
            firstName = faker.Name.FirstName(),
            lastName = faker.Name.LastName(),
        });
        register.EnsureSuccessStatusCode();

        await Authorize("manager@company.com", "Manager@123");
        var projects = await _client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects?status=Active", Json);
        var projectId = projects!.Items[0].Id;

        var titles = new Faker<CreateTaskRequest>()
            .UseSeed(99)
            .RuleFor(t => t.Title, f => f.Hacker.Verb() + " " + f.Hacker.Noun())
            .RuleFor(t => t.Description, f => f.Lorem.Sentence())
            .RuleFor(t => t.ProjectId, projectId)
            .RuleFor(t => t.Priority, f => f.PickRandom<TaskPriority>())
            .RuleFor(t => t.EstimatedHours, f => f.Random.Int(1, 40))
            .Generate(10);

        foreach (var body in titles)
        {
            var res = await _client.PostAsJsonAsync("/api/tasks", body, Json);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        }

        var stats = await _client.GetFromJsonAsync<TaskStatisticsDto>("/api/tasks/statistics", Json);
        Assert.Equal(15, stats!.TotalTasks); // 5 seeded + 10 generated
    }

    [Fact]
    public async Task Reset_between_tests_is_isolated()
    {
        // This test would see 15 tasks if the previous test's writes leaked through.
        await Authorize("manager@company.com", "Manager@123");
        var stats = await _client.GetFromJsonAsync<TaskStatisticsDto>("/api/tasks/statistics", Json);
        Assert.Equal(5, stats!.TotalTasks);
    }
}
