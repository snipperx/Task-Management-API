using System.Security.Claims;
using TaskManagementAPI.Common;
using TaskManagementAPI.Domain;

namespace TaskManagementAPI.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid Id { get; }
    string Email { get; }
    UserRole Role { get; }
    bool HasPermission(string permission);
    bool IsAdminOrManager { get; }
}

public class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? _principal;

    public CurrentUser(IHttpContextAccessor accessor) => _principal = accessor.HttpContext?.User;

    public bool IsAuthenticated => _principal?.Identity?.IsAuthenticated ?? false;

    public Guid Id
    {
        get
        {
            var raw = _principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? _principal?.FindFirstValue("sub");
            return Guid.TryParse(raw, out var id)
                ? id
                : throw new ForbiddenException("The current request has no authenticated user.");
        }
    }

    public string Email => _principal?.FindFirstValue(ClaimTypes.Email)
                           ?? _principal?.FindFirstValue("email")
                           ?? string.Empty;

    public UserRole Role =>
        Enum.TryParse<UserRole>(_principal?.FindFirstValue("role"), out var role) ? role : UserRole.Viewer;

    public bool HasPermission(string permission) => RolePermissions.Has(Role, permission);

    public bool IsAdminOrManager => Role is UserRole.Admin or UserRole.Manager;
}
