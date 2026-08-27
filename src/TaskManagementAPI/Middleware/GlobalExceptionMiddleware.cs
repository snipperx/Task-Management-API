using System.Text.Json;
using TaskManagementAPI.Common;

namespace TaskManagementAPI.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public GlobalExceptionMiddleware(
        RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.TraceIdentifier;

        var error = new ErrorResponse
        {
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow
        };

        switch (exception)
        {
            case ValidationException v:
                error.Status = v.StatusCode;
                error.Message = v.Message;
                error.Errors = v.Errors.Count > 0 ? v.Errors : null;
                _logger.LogWarning("Validation failure {CorrelationId}: {Message}", correlationId, v.Message);
                break;

            case AppException app:
                error.Status = app.StatusCode;
                error.Message = app.Message;
                _logger.LogWarning("Handled {Type} {CorrelationId}: {Message}",
                    app.GetType().Name, correlationId, app.Message);
                break;

            default:
                error.Status = StatusCodes.Status500InternalServerError;
                error.Message = _env.IsDevelopment()
                    ? exception.ToString()
                    : "An unexpected error occurred. Please contact support with the correlation id.";
                _logger.LogError(exception, "Unhandled exception {CorrelationId}", correlationId);
                break;
        }

        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Response already started; cannot write error body for {CorrelationId}", correlationId);
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = error.Status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(error, JsonOptions));
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();
}
