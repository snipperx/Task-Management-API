using Microsoft.EntityFrameworkCore;
using TaskManagementAPI.Data;
using TaskManagementAPI.Domain;

namespace TaskManagementAPI.Services;

/// <summary>
/// Periodically raises the priority of overdue, not-Done tasks (one step, capped at Critical).
/// Implements the "task priority automatically increases if overdue" rule.
/// </summary>
public class OverdueEscalationService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OverdueEscalationService> _logger;

    public OverdueEscalationService(IServiceScopeFactory scopeFactory, ILogger<OverdueEscalationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await EscalateAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Overdue escalation sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EscalateAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateTime.UtcNow.Date;
        var overdue = await db.Tasks
            .Where(t => t.DueDate != null
                        && t.Status != WorkItemStatus.Done
                        && t.DueDate < today
                        && t.Priority != TaskPriority.Critical)
            .ToListAsync(ct);

        if (overdue.Count == 0) return;

        foreach (var task in overdue)
            task.Priority = TaskWorkflow.Escalate(task.Priority);

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Escalated priority for {Count} overdue task(s)", overdue.Count);
    }
}
