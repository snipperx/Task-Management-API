using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Domain;
using TaskManagementAPI.Security;
using TaskManagementAPI.Services;
using TaskManagementAPI.Tests.TestSupport;
using Xunit;

namespace TaskManagementAPI.Tests.Unit;

public class AuthServiceTests
{
    private static readonly JwtSettings Jwt = new()
    {
        Secret = "unit-test-signing-key-that-is-definitely-long-enough-1234",
        Issuer = "test",
        Audience = "test",
        AccessTokenExpirationMinutes = 15,
        RefreshTokenExpirationDays = 7
    };

    private static AuthService Build(TestDb db)
    {
        var hasher = new BCryptPasswordHasher();
        return new AuthService(
            db.Repo<User>(), db.Uow(),
            new TokenService(Options.Create(Jwt)),
            hasher,
            Options.Create(Jwt),
            NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task RegisterAsync_creates_developer_and_returns_tokens()
    {
        using var db = new TestDb();
        var svc = Build(db);

        var res = await svc.RegisterAsync(new RegisterRequest
        {
            Email = "New.User@Test.LOCAL",
            Password = "Sup3rSecret!",
            FirstName = "New",
            LastName = "User",
            Role = UserRole.Admin // must be ignored
        });

        Assert.Equal(UserRole.Developer, res.User.Role);
        Assert.False(string.IsNullOrWhiteSpace(res.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(res.RefreshToken));
        Assert.Equal("new.user@test.local", res.User.Email);
    }

    [Fact]
    public async Task RegisterAsync_rejects_duplicate_email()
    {
        using var db = new TestDb();
        var svc = Build(db);
        var req = new RegisterRequest { Email = "dup@test.local", Password = "Sup3rSecret!", FirstName = "A", LastName = "B" };
        await svc.RegisterAsync(req);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.RegisterAsync(req));
    }

    [Fact]
    public async Task LoginAsync_wrong_password_throws_Unauthorized()
    {
        using var db = new TestDb();
        var svc = Build(db);
        await svc.RegisterAsync(new RegisterRequest
        {
            Email = "a@test.local", Password = "Sup3rSecret!", FirstName = "A", LastName = "B"
        });

        await Assert.ThrowsAsync<UnauthorizedException>(() => svc.LoginAsync(new LoginRequest
        {
            Email = "a@test.local", Password = "wrong"
        }));
    }

    [Fact]
    public async Task ChangePasswordAsync_requires_correct_current_password()
    {
        using var db = new TestDb();
        var svc = Build(db);
        var reg = await svc.RegisterAsync(new RegisterRequest
        {
            Email = "c@test.local", Password = "OldPass123!", FirstName = "A", LastName = "B"
        });

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.ChangePasswordAsync(
            reg.User.Id, new ChangePasswordRequest { CurrentPassword = "nope", NewPassword = "NewPass123!" }));

        await svc.ChangePasswordAsync(reg.User.Id,
            new ChangePasswordRequest { CurrentPassword = "OldPass123!", NewPassword = "NewPass123!" });

        var login = await svc.LoginAsync(new LoginRequest { Email = "c@test.local", Password = "NewPass123!" });
        Assert.NotNull(login.AccessToken);
    }

    [Fact]
    public async Task RefreshAsync_rejects_unknown_refresh_token()
    {
        using var db = new TestDb();
        var svc = Build(db);
        var reg = await svc.RegisterAsync(new RegisterRequest
        {
            Email = "r@test.local", Password = "Sup3rSecret!", FirstName = "A", LastName = "B"
        });

        await Assert.ThrowsAsync<UnauthorizedException>(() => svc.RefreshAsync(new RefreshRequest
        {
            AccessToken = reg.AccessToken, RefreshToken = "not-the-real-token"
        }));
    }
}
