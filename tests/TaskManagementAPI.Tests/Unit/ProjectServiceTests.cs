using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Domain;
using TaskManagementAPI.Services;
using TaskManagementAPI.Tests.TestSupport;
using Xunit;

namespace TaskManagementAPI.Tests.Unit;

public class ProjectServiceTests
{
    private static ProjectService Build(TestDb db)
        => new(db.Repo<Project>(), db.Repo<TaskItem>(), db.Uow());

    [Fact]
    public async Task DeleteAsync_blocks_when_project_has_active_tasks()
    {
        using var db = new TestDb();
        var admin = db.AddUser(UserRole.Admin);
        var project = db.AddProject(admin.Id);
        db.AddTask(project.Id, admin.Id, status: WorkItemStatus.InProgress);
        var svc = Build(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.DeleteAsync(project.Id));
    }

    [Fact]
    public async Task DeleteAsync_succeeds_when_all_tasks_done()
    {
        using var db = new TestDb();
        var admin = db.AddUser(UserRole.Admin);
        var project = db.AddProject(admin.Id);
        db.AddTask(project.Id, admin.Id, status: WorkItemStatus.Done);
        var svc = Build(db);

        await svc.DeleteAsync(project.Id);

        Assert.False(await db.Repo<Project>().AnyAsync(p => p.Id == project.Id));
    }

    [Fact]
    public async Task UpdateAsync_rejects_modifying_archived_project()
    {
        using var db = new TestDb();
        var admin = db.AddUser(UserRole.Admin);
        var project = db.AddProject(admin.Id, ProjectStatus.Archived);
        var svc = Build(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.UpdateAsync(project.Id, new UpdateProjectRequest
        {
            Name = "Renamed", Status = ProjectStatus.Active
        }));
    }

    [Fact]
    public async Task UpdateAsync_completing_project_stamps_completedAt()
    {
        using var db = new TestDb();
        var admin = db.AddUser(UserRole.Admin);
        var project = db.AddProject(admin.Id);
        var svc = Build(db);

        var dto = await svc.UpdateAsync(project.Id, new UpdateProjectRequest
        {
            Name = project.Name, Status = ProjectStatus.Completed
        });

        Assert.Equal(ProjectStatus.Completed, dto.Status);
        Assert.NotNull(dto.CompletedAt);
    }

    [Fact]
    public async Task GetStatisticsAsync_computes_completion_rate()
    {
        using var db = new TestDb();
        var admin = db.AddUser(UserRole.Admin);
        var project = db.AddProject(admin.Id);
        db.AddTask(project.Id, admin.Id, status: WorkItemStatus.Done);
        db.AddTask(project.Id, admin.Id, status: WorkItemStatus.Done);
        db.AddTask(project.Id, admin.Id, status: WorkItemStatus.ToDo);
        db.AddTask(project.Id, admin.Id, status: WorkItemStatus.InProgress);
        var svc = Build(db);

        var stats = await svc.GetStatisticsAsync(project.Id);

        Assert.Equal(4, stats.TotalTasks);
        Assert.Equal(2, stats.CompletedTasks);
        Assert.Equal(50d, stats.CompletionRate);
    }
}
