using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Respawn;
using Respawn.Graph;
using TaskManagementAPI.Data;
using Testcontainers.PostgreSql;

namespace TaskManagementAPI.Tests.Integration;

/// <summary>
/// Boots the API against a throw-away PostgreSQL container (Testcontainers) so integration
/// tests run the real Npgsql provider, real EF migrations and the real <see cref="DataSeeder"/>
/// — the things EF-InMemory can't exercise. <see cref="ResetAsync"/> truncates all data with
/// Respawn and re-seeds, giving each test a clean, seeded database.
///
/// Requires a working Docker daemon. The container is started once per test collection.
/// </summary>
public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("taskmanagement_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private NpgsqlConnection _connection = null!;
    private Respawner _respawner = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "postgres-integration-signing-key-long-enough-000000",
                ["SeedOnStartup"] = "true",
                ["Swagger:Enabled"] = "false",
            });
        });

        builder.ConfigureServices(services =>
        {
            // AddPersistence() reads the connection string eagerly while the app builds — before
            // the config overrides above apply — so point EF at the container by swapping the
            // DbContext registration here instead (ConfigureServices runs late, container is up).
            var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    (d.ServiceType.FullName?.StartsWith("Npgsql.EntityFrameworkCore", StringComparison.Ordinal) ?? false))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_postgres.GetConnectionString()));

            // Drop the background escalation sweep so it doesn't race the tests.
            services.RemoveAll<IHostedService>();
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Touch the host so the app builds, migrates and seeds before the first test.
        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.CanConnectAsync();
        }

        _connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await _connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Table("__EFMigrationsHistory")],
        });
    }

    /// <summary>Truncate every table, then rebuild the standard seed data.</summary>
    public async Task ResetAsync()
    {
        await _respawner.ResetAsync(_connection);
        await DataSeeder.SeedAsync(Services);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresApiFactory>
{
    public const string Name = "postgres";
}
