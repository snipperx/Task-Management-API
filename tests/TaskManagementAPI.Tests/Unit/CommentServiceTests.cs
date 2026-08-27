using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Domain;
using TaskManagementAPI.Services;
using TaskManagementAPI.Tests.TestSupport;
using Xunit;

namespace TaskManagementAPI.Tests.Unit;

public class CommentServiceTests
{
    private static CommentService Build(TestDb db, Guid actorId, UserRole role)
        => new(db.Repo<TaskComment>(), db.Repo<TaskItem>(), db.Uow(), new FakeCurrentUser(actorId, role));

    [Fact]
    public async Task AddAsync_creates_comment_for_current_user()
    {
        using var db = new TestDb();
        var dev = db.AddUser(UserRole.Developer);
        var project = db.AddProject(dev.Id);
        var task = db.AddTask(project.Id, dev.Id);
        var svc = Build(db, dev.Id, UserRole.Developer);

        var dto = await svc.AddAsync(task.Id, new CreateCommentRequest { Content = "  hello  " });

        Assert.Equal("hello", dto.Content);
        Assert.Equal(dev.Id, dto.UserId);
        Assert.Equal(dev.FullName, dto.UserName);
    }

    [Fact]
    public async Task AddAsync_unknown_task_throws_NotFound()
    {
        using var db = new TestDb();
        var dev = db.AddUser(UserRole.Developer);
        var svc = Build(db, dev.Id, UserRole.Developer);

        await Assert.ThrowsAsync<NotFoundException>(
            () => svc.AddAsync(Guid.NewGuid(), new CreateCommentRequest { Content = "x" }));
    }

    [Fact]
    public async Task GetForTaskAsync_returns_comments_oldest_first()
    {
        using var db = new TestDb();
        var dev = db.AddUser(UserRole.Developer);
        var project = db.AddProject(dev.Id);
        var task = db.AddTask(project.Id, dev.Id);
        var svc = Build(db, dev.Id, UserRole.Developer);

        await svc.AddAsync(task.Id, new CreateCommentRequest { Content = "first" });
        await svc.AddAsync(task.Id, new CreateCommentRequest { Content = "second" });

        var list = await svc.GetForTaskAsync(task.Id);

        Assert.Collection(list,
            c => Assert.Equal("first", c.Content),
            c => Assert.Equal("second", c.Content));
    }

    [Fact]
    public async Task UpdateAsync_by_author_sets_updatedAt()
    {
        using var db = new TestDb();
        var dev = db.AddUser(UserRole.Developer);
        var project = db.AddProject(dev.Id);
        var task = db.AddTask(project.Id, dev.Id);
        var svc = Build(db, dev.Id, UserRole.Developer);
        var created = await svc.AddAsync(task.Id, new CreateCommentRequest { Content = "orig" });

        var updated = await svc.UpdateAsync(created.Id, new UpdateCommentRequest { Content = "edited" });

        Assert.Equal("edited", updated.Content);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_by_other_developer_is_forbidden()
    {
        using var db = new TestDb();
        var author = db.AddUser(UserRole.Developer);
        var intruder = db.AddUser(UserRole.Developer);
        var project = db.AddProject(author.Id);
        var task = db.AddTask(project.Id, author.Id);
        var created = await Build(db, author.Id, UserRole.Developer)
            .AddAsync(task.Id, new CreateCommentRequest { Content = "mine" });

        var intruderSvc = Build(db, intruder.Id, UserRole.Developer);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => intruderSvc.UpdateAsync(created.Id, new UpdateCommentRequest { Content = "hijack" }));
    }

    [Fact]
    public async Task Manager_can_moderate_other_users_comment()
    {
        using var db = new TestDb();
        var author = db.AddUser(UserRole.Developer);
        var manager = db.AddUser(UserRole.Manager);
        var project = db.AddProject(author.Id);
        var task = db.AddTask(project.Id, author.Id);
        var created = await Build(db, author.Id, UserRole.Developer)
            .AddAsync(task.Id, new CreateCommentRequest { Content = "spam" });

        var managerSvc = Build(db, manager.Id, UserRole.Manager);
        await managerSvc.DeleteAsync(created.Id);

        Assert.False(await db.Repo<TaskComment>().AnyAsync(c => c.Id == created.Id));
    }

    [Fact]
    public async Task DeleteAsync_unknown_comment_throws_NotFound()
    {
        using var db = new TestDb();
        var dev = db.AddUser(UserRole.Developer);
        var svc = Build(db, dev.Id, UserRole.Developer);

        await Assert.ThrowsAsync<NotFoundException>(() => svc.DeleteAsync(Guid.NewGuid()));
    }
}
