using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Domain;
using TaskManagementAPI.Services;
using TaskManagementAPI.Tests.TestSupport;
using Xunit;

namespace TaskManagementAPI.Tests.Unit;

public class TaskServiceTests
{
    private static TaskService Build(TestDb db, Guid actorId, UserRole role)
        => new(
            db.Repo<TaskItem>(), db.Repo<Project>(), db.Repo<User>(), db.Uow(),
            new FakeCurrentUser(actorId, role),
            NullLogger<TaskService>.Instance);

    [Fact]
    public async Task CreateAsync_persists_task_in_ToDo()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var project = db.AddProject(manager.Id);
        var svc = Build(db, manager.Id, UserRole.Manager);

        var dto = await svc.CreateAsync(new CreateTaskRequest
        {
            Title = "Ship it",
            ProjectId = project.Id,
            Priority = TaskPriority.High,
            EstimatedHours = 5
        });

        Assert.Equal(WorkItemStatus.ToDo, dto.Status);
        Assert.Equal(manager.Id, dto.CreatedBy);
        Assert.True(await db.Repo<TaskItem>().AnyAsync(t => t.Id == dto.Id));
    }

    [Fact]
    public async Task CreateAsync_rejects_task_in_non_active_project()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var project = db.AddProject(manager.Id, ProjectStatus.Archived);
        var svc = Build(db, manager.Id, UserRole.Manager);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CreateAsync(new CreateTaskRequest
        {
            Title = "Nope", ProjectId = project.Id, Priority = TaskPriority.Low
        }));
    }

    [Fact]
    public async Task CreateAsync_missing_project_throws_NotFound()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var svc = Build(db, manager.Id, UserRole.Manager);

        await Assert.ThrowsAsync<NotFoundException>(() => svc.CreateAsync(new CreateTaskRequest
        {
            Title = "Ghost", ProjectId = Guid.NewGuid(), Priority = TaskPriority.Low
        }));
    }

    [Fact]
    public async Task ChangeStatusAsync_rejects_illegal_transition()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var project = db.AddProject(manager.Id);
        var task = db.AddTask(project.Id, manager.Id, status: WorkItemStatus.ToDo);
        var svc = Build(db, manager.Id, UserRole.Manager);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.ChangeStatusAsync(task.Id, WorkItemStatus.Done));
    }

    [Fact]
    public async Task ChangeStatusAsync_allows_legal_step_and_stamps_completion()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var project = db.AddProject(manager.Id);
        var task = db.AddTask(project.Id, manager.Id, assignedTo: manager.Id, status: WorkItemStatus.InReview);
        var svc = Build(db, manager.Id, UserRole.Manager);

        var dto = await svc.ChangeStatusAsync(task.Id, WorkItemStatus.Done);

        Assert.Equal(WorkItemStatus.Done, dto.Status);
        Assert.NotNull(dto.CompletedAt);
    }

    [Fact]
    public async Task Developer_cannot_modify_task_not_assigned_to_them()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var dev = db.AddUser(UserRole.Developer);
        var project = db.AddProject(manager.Id);
        var task = db.AddTask(project.Id, manager.Id, assignedTo: manager.Id);
        var svc = Build(db, dev.Id, UserRole.Developer);

        await Assert.ThrowsAsync<ForbiddenException>(() => svc.UpdateAsync(task.Id, new UpdateTaskRequest
        {
            Title = "Grabbed", EstimatedHours = 1, ActualHours = 0
        }));
    }

    [Fact]
    public async Task ChangeStatusAsync_enforces_max_in_progress_per_user()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var dev = db.AddUser(UserRole.Developer);
        var project = db.AddProject(manager.Id);

        for (var i = 0; i < TaskService.MaxInProgressPerUser; i++)
            db.AddTask(project.Id, manager.Id, assignedTo: dev.Id, status: WorkItemStatus.InProgress);

        var eleventh = db.AddTask(project.Id, manager.Id, assignedTo: dev.Id, status: WorkItemStatus.ToDo);
        var svc = Build(db, dev.Id, UserRole.Developer);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => svc.ChangeStatusAsync(eleventh.Id, WorkItemStatus.InProgress));
        Assert.Contains("In Progress", ex.Message);
    }

    [Fact]
    public async Task GetAsync_filters_sorts_and_paginates()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var dev = db.AddUser(UserRole.Developer);
        var project = db.AddProject(manager.Id);

        db.AddTask(project.Id, manager.Id, assignedTo: dev.Id, status: WorkItemStatus.InProgress, priority: TaskPriority.High);
        db.AddTask(project.Id, manager.Id, assignedTo: dev.Id, status: WorkItemStatus.InProgress, priority: TaskPriority.Low);
        db.AddTask(project.Id, manager.Id, status: WorkItemStatus.ToDo, priority: TaskPriority.Critical);
        var svc = Build(db, manager.Id, UserRole.Manager);

        var inProgress = await svc.GetAsync(new TaskQuery { Status = WorkItemStatus.InProgress, AssigneeId = dev.Id });
        Assert.Equal(2, inProgress.TotalCount);

        var sorted = await svc.GetAsync(new TaskQuery { ProjectId = project.Id, Sort = "-priority", PageSize = 2 });
        Assert.Equal(3, sorted.TotalCount);
        Assert.Equal(2, sorted.Items.Count);
        Assert.Equal(TaskPriority.Critical, sorted.Items[0].Priority);

        var search = await svc.GetAsync(new TaskQuery { Search = sorted.Items[0].Title[..8] });
        Assert.True(search.TotalCount >= 1);
    }

    [Fact]
    public async Task GetOverdueAsync_returns_only_open_past_due_tasks()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var project = db.AddProject(manager.Id);
        db.AddTask(project.Id, manager.Id, dueDate: DateTime.UtcNow.Date.AddDays(-2));
        db.AddTask(project.Id, manager.Id, status: WorkItemStatus.Done, dueDate: DateTime.UtcNow.Date.AddDays(-2));
        db.AddTask(project.Id, manager.Id, dueDate: DateTime.UtcNow.Date.AddDays(3));
        var svc = Build(db, manager.Id, UserRole.Manager);

        var overdue = await svc.GetOverdueAsync();

        Assert.Single(overdue);
        Assert.True(overdue[0].IsOverdue);
    }

    [Fact]
    public async Task AssignAsync_can_assign_and_unassign()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var dev = db.AddUser(UserRole.Developer);
        var project = db.AddProject(manager.Id);
        var task = db.AddTask(project.Id, manager.Id);
        var svc = Build(db, manager.Id, UserRole.Manager);

        var assigned = await svc.AssignAsync(task.Id, dev.Id);
        Assert.Equal(dev.Id, assigned.AssignedTo);

        var unassigned = await svc.AssignAsync(task.Id, null);
        Assert.Null(unassigned.AssignedTo);
    }

    [Fact]
    public async Task AssignAsync_unknown_assignee_throws_Validation()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var project = db.AddProject(manager.Id);
        var task = db.AddTask(project.Id, manager.Id);
        var svc = Build(db, manager.Id, UserRole.Manager);

        await Assert.ThrowsAsync<ValidationException>(() => svc.AssignAsync(task.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task ChangePriorityAsync_updates_priority()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var project = db.AddProject(manager.Id);
        var task = db.AddTask(project.Id, manager.Id, priority: TaskPriority.Low);
        var svc = Build(db, manager.Id, UserRole.Manager);

        var dto = await svc.ChangePriorityAsync(task.Id, TaskPriority.Critical);

        Assert.Equal(TaskPriority.Critical, dto.Priority);
    }

    [Fact]
    public async Task GetStatisticsAsync_scopes_to_project_when_given()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var p1 = db.AddProject(manager.Id);
        var p2 = db.AddProject(manager.Id);
        db.AddTask(p1.Id, manager.Id, status: WorkItemStatus.Done);
        db.AddTask(p1.Id, manager.Id, status: WorkItemStatus.ToDo);
        db.AddTask(p2.Id, manager.Id, status: WorkItemStatus.ToDo);
        var svc = Build(db, manager.Id, UserRole.Manager);

        var scoped = await svc.GetStatisticsAsync(p1.Id);
        Assert.Equal(2, scoped.TotalTasks);
        Assert.Equal(50d, scoped.CompletionRate);

        var all = await svc.GetStatisticsAsync(null);
        Assert.Equal(3, all.TotalTasks);
    }

    [Fact]
    public async Task CreateAsync_past_due_date_throws_Validation()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var project = db.AddProject(manager.Id);
        var svc = Build(db, manager.Id, UserRole.Manager);

        await Assert.ThrowsAsync<ValidationException>(() => svc.CreateAsync(new CreateTaskRequest
        {
            Title = "late", ProjectId = project.Id, Priority = TaskPriority.Low,
            DueDate = DateTime.UtcNow.Date.AddDays(-1)
        }));
    }

    [Fact]
    public async Task DeleteAsync_is_soft_delete()
    {
        using var db = new TestDb();
        var manager = db.AddUser(UserRole.Manager);
        var project = db.AddProject(manager.Id);
        var task = db.AddTask(project.Id, manager.Id);
        var svc = Build(db, manager.Id, UserRole.Manager);

        await svc.DeleteAsync(task.Id);

        Assert.False(await db.Repo<TaskItem>().AnyAsync(t => t.Id == task.Id)); // hidden by query filter
        Assert.True(db.Context.Tasks.IgnoreQueryFilters().Any(t => t.Id == task.Id && t.IsDeleted));
    }
}
