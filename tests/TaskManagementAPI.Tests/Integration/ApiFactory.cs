using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TaskManagementAPI.Data;
using TaskManagementAPI.Services;

namespace TaskManagementAPI.Tests.Integration;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"api-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "integration-test-signing-key-long-enough-000000000000",
                ["ConnectionStrings:DefaultConnection"] = "unused-in-memory",
                ["SeedOnStartup"] = "true",
                ["Swagger:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Strip every EF Core / Npgsql registration so only the InMemory provider remains.
            var toRemove = services.Where(d =>
                    d.ServiceType.FullName is { } n &&
                    (n.Contains("EntityFrameworkCore", StringComparison.Ordinal) ||
                     n.Contains("Npgsql", StringComparison.Ordinal) ||
                     d.ServiceType == typeof(AppDbContext)))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.RemoveAll<IHostedService>(); // drop background escalation sweep

            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }
}
