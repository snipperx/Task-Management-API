using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Domain;
using TaskManagementAPI.Services;
using TaskManagementAPI.Tests.TestSupport;
using Xunit;

namespace TaskManagementAPI.Tests.Unit;

public class UserServiceTests
{
    private static UserService Build(TestDb db) => new(db.Repo<User>(), db.Uow());

    [Fact]
    public async Task GetUsersAsync_paginates()
    {
        using var db = new TestDb();
        for (var i = 0; i < 7; i++) db.AddUser(UserRole.Developer);
        var svc = Build(db);

        var page = await svc.GetUsersAsync(new PageQuery { PageNumber = 2, PageSize = 3 });

        Assert.Equal(7, page.TotalCount);
        Assert.Equal(3, page.PageSize);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPrevious);
        Assert.True(page.HasNext);
        Assert.Equal(3, page.Items.Count);
    }

    [Fact]
    public async Task GetByIdAsync_unknown_throws_NotFound()
    {
        using var db = new TestDb();
        await Assert.ThrowsAsync<NotFoundException>(() => Build(db).GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateAsync_changes_name_and_active_flag()
    {
        using var db = new TestDb();
        var user = db.AddUser(UserRole.Developer);
        var svc = Build(db);

        var dto = await svc.UpdateAsync(user.Id, new UpdateUserRequest
        {
            FirstName = "Renamed", LastName = "Person", IsActive = false
        });

        Assert.Equal("Renamed Person", dto.FullName);
        Assert.False(dto.IsActive);
    }

    [Fact]
    public async Task DeleteAsync_deactivates_and_clears_refresh_token()
    {
        using var db = new TestDb();
        var target = db.AddUser(UserRole.Developer);
        target.RefreshToken = "tok";
        target.RefreshTokenExpiry = DateTime.UtcNow.AddDays(1);
        db.Context.SaveChanges();
        var admin = db.AddUser(UserRole.Admin);
        var svc = Build(db);

        await svc.DeleteAsync(target.Id, admin.Id);

        var reloaded = await db.Repo<User>().GetByIdAsync(target.Id);
        Assert.False(reloaded!.IsActive);
        Assert.Null(reloaded.RefreshToken);
    }

    [Fact]
    public async Task DeleteAsync_self_is_rejected()
    {
        using var db = new TestDb();
        var admin = db.AddUser(UserRole.Admin);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Build(db).DeleteAsync(admin.Id, admin.Id));
    }

    [Fact]
    public async Task AssignRoleAsync_updates_role_and_invalidates_sessions()
    {
        using var db = new TestDb();
        var user = db.AddUser(UserRole.Developer);
        user.RefreshToken = "tok";
        db.Context.SaveChanges();
        var svc = Build(db);

        var dto = await svc.AssignRoleAsync(user.Id, UserRole.Manager);

        Assert.Equal(UserRole.Manager, dto.Role);
        var reloaded = await db.Repo<User>().GetByIdAsync(user.Id);
        Assert.Null(reloaded!.RefreshToken);
    }
}
