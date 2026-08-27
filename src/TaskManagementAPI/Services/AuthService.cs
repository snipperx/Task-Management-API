using Microsoft.EntityFrameworkCore;
using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Domain;
using TaskManagementAPI.Repositories;
using TaskManagementAPI.Security;

namespace TaskManagementAPI.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task LogoutAsync(Guid userId, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher _hasher;
    private readonly JwtSettings _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IRepository<User> users,
        IUnitOfWork uow,
        ITokenService tokens,
        IPasswordHasher hasher,
        Microsoft.Extensions.Options.IOptions<JwtSettings> jwt,
        ILogger<AuthService> logger)
    {
        _users = users;
        _uow = uow;
        _tokens = tokens;
        _hasher = hasher;
        _jwt = jwt.Value;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _users.AnyAsync(u => u.Email == email, ct))
            throw new BusinessRuleException("An account with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = UserRole.Developer, // self-registration is always Developer; role changes go through UsersController
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _users.AddAsync(user, ct);
        await IssueTokensAsync(user, ct);

        _logger.LogInformation("New user registered {UserId}", user.Id);
        return BuildResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.Query(tracking: true).FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        if (!user.IsActive)
            throw new ForbiddenException("This account has been deactivated.");

        await IssueTokensAsync(user, ct);
        return BuildResponse(user);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var principal = _tokens.GetPrincipalFromExpiredToken(request.AccessToken)
            ?? throw new UnauthorizedException("Invalid access token.");

        var sub = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? principal.FindFirst("sub")?.Value;

        if (!Guid.TryParse(sub, out var userId))
            throw new UnauthorizedException("Invalid access token.");

        var user = await _users.Query(tracking: true).FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new UnauthorizedException("Invalid access token.");

        if (user.RefreshToken is null ||
            user.RefreshToken != request.RefreshToken ||
            user.RefreshTokenExpiry is null ||
            user.RefreshTokenExpiry <= DateTime.UtcNow)
        {
            throw new UnauthorizedException("Refresh token is invalid or expired.");
        }

        await IssueTokensAsync(user, ct);
        return BuildResponse(user);
    }

    public async Task LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.Query(tracking: true).FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return;

        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _users.Query(tracking: true).FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User", userId);

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new BusinessRuleException("The current password is incorrect.");

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        user.RefreshToken = null; // force re-login elsewhere
        user.RefreshTokenExpiry = null;
        await _uow.SaveChangesAsync(ct);
    }

    private async Task IssueTokensAsync(User user, CancellationToken ct)
    {
        user.RefreshToken = _tokens.GenerateRefreshToken();
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays);
        await _uow.SaveChangesAsync(ct);
    }

    private AuthResponse BuildResponse(User user)
    {
        var (accessToken, expiresAt) = _tokens.GenerateAccessToken(user);
        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = user.RefreshToken!,
            AccessTokenExpiresAt = expiresAt,
            User = user.ToDto()
        };
    }
}
