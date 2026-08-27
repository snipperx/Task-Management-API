using Microsoft.EntityFrameworkCore;
using TaskManagementAPI.Data;
using TaskManagementAPI.Domain;

namespace TaskManagementAPI.Services;

/// <summary>Raises the priority of overdue, not-Done tasks one step (capped at Critical).</summary>
public interface IOverdueTaskEscalator
{
    Task<int> EscalateAsync(CancellationToken ct = default);
}

public class OverdueTaskEscalator : IOverdueTaskEscalator
{
    private readonly AppDbContext _db;
    private readonly ILogger<OverdueTaskEscalator> _logger;

    public OverdueTaskEscalator(AppDbContext db, ILogger<OverdueTaskEscalator> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> EscalateAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var overdue = await _db.Tasks
            .Where(t => t.DueDate != null
                        && t.Status != WorkItemStatus.Done
                        && t.DueDate < today
                        && t.Priority != TaskPriority.Critical)
            .ToListAsync(ct);

        if (overdue.Count == 0) return 0;

        foreach (var task in overdue)
            task.Priority = TaskWorkflow.Escalate(task.Priority);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Escalated priority for {Count} overdue task(s)", overdue.Count);
        return overdue.Count;
    }
}

/// <summary>
/// Periodically runs <see cref="IOverdueTaskEscalator"/> — implements the
/// "task priority automatically increases if overdue" rule.
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
                using var scope = _scopeFactory.CreateScope();
                var escalator = scope.ServiceProvider.GetRequiredService<IOverdueTaskEscalator>();
                await escalator.EscalateAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Overdue escalation sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
