using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json.Serialization;
using TaskManagementAPI.Data;
using TaskManagementAPI.Extensions;
using TaskManagementAPI.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    // Return the shared ErrorResponse shape for model-validation failures.
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        var payload = new TaskManagementAPI.Common.ErrorResponse
        {
            CorrelationId = context.HttpContext.TraceIdentifier,
            Status = StatusCodes.Status400BadRequest,
            Message = "One or more validation errors occurred.",
            Errors = errors
        };
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(payload);
    };
});

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddSwagger();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length > 0)
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
        else
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment() ||
    app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Task Management API v1"));
}

// HTTPS is expected to be terminated by the hosting platform / reverse proxy in front of the
// container. Enable redirection only when this process is actually bound to an HTTPS port.
if (!string.IsNullOrEmpty(app.Configuration["ASPNETCORE_HTTPS_PORTS"])
    || !string.IsNullOrEmpty(app.Configuration["HTTPS_PORT"]))
{
    app.UseHttpsRedirection();
}

app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() })
        });
    }
});

if (app.Configuration.GetValue("SeedOnStartup", true))
{
    await DataSeeder.SeedAsync(app.Services);
}

app.Run();

public partial class Program { }
