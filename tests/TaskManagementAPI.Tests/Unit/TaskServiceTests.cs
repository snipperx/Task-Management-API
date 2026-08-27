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
