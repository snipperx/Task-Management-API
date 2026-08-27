using Microsoft.Extensions.Logging.Abstractions;
using TaskManagementAPI.Domain;
using TaskManagementAPI.Services;
using TaskManagementAPI.Tests.TestSupport;
using Xunit;

namespace TaskManagementAPI.Tests.Unit;

public class OverdueTaskEscalatorTests
{
    [Fact]
    public async Task EscalateAsync_bumps_only_overdue_open_non_critical_tasks()
    {
        using var db = new TestDb();
        var user = db.AddUser(UserRole.Manager);
        var project = db.AddProject(user.Id);

        var overdueMedium = db.AddTask(project.Id, user.Id, status: WorkItemStatus.ToDo,
            priority: TaskPriority.Medium, dueDate: DateTime.UtcNow.Date.AddDays(-3));
        var overdueCritical = db.AddTask(project.Id, user.Id, status: WorkItemStatus.InProgress,
            priority: TaskPriority.Critical, dueDate: DateTime.UtcNow.Date.AddDays(-1));
        var overdueButDone = db.AddTask(project.Id, user.Id, status: WorkItemStatus.Done,
            priority: TaskPriority.Low, dueDate: DateTime.UtcNow.Date.AddDays(-5));
        var futureTask = db.AddTask(project.Id, user.Id, status: WorkItemStatus.ToDo,
            priority: TaskPriority.Low, dueDate: DateTime.UtcNow.Date.AddDays(2));

        var escalator = new OverdueTaskEscalator(db.Context, NullLogger<OverdueTaskEscalator>.Instance);

        var count = await escalator.EscalateAsync();

        Assert.Equal(1, count);
        Assert.Equal(TaskPriority.High, (await db.Repo<TaskItem>().GetByIdAsync(overdueMedium.Id))!.Priority);
        Assert.Equal(TaskPriority.Critical, (await db.Repo<TaskItem>().GetByIdAsync(overdueCritical.Id))!.Priority);
        Assert.Equal(TaskPriority.Low, (await db.Repo<TaskItem>().GetByIdAsync(overdueButDone.Id))!.Priority);
        Assert.Equal(TaskPriority.Low, (await db.Repo<TaskItem>().GetByIdAsync(futureTask.Id))!.Priority);
    }

    [Fact]
    public async Task EscalateAsync_returns_zero_when_nothing_overdue()
    {
        using var db = new TestDb();
        var user = db.AddUser(UserRole.Manager);
        var project = db.AddProject(user.Id);
        db.AddTask(project.Id, user.Id, dueDate: DateTime.UtcNow.Date.AddDays(5));

        var escalator = new OverdueTaskEscalator(db.Context, NullLogger<OverdueTaskEscalator>.Instance);

        Assert.Equal(0, await escalator.EscalateAsync());
    }
}
