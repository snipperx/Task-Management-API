using Microsoft.EntityFrameworkCore;
using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Domain;
using TaskManagementAPI.Repositories;

namespace TaskManagementAPI.Services;

public interface IUserService
{
    Task<PagedResult<UserDto>> GetUsersAsync(PageQuery query, CancellationToken ct = default);
    Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid currentUserId, CancellationToken ct = default);
    Task<UserDto> AssignRoleAsync(Guid id, UserRole role, CancellationToken ct = default);
}

public class UserService : IUserService
{
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;

    public UserService(IRepository<User> users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<PagedResult<UserDto>> GetUsersAsync(PageQuery query, CancellationToken ct = default)
    {
        var q = _users.Query().OrderBy(u => u.FirstName).ThenBy(u => u.LastName);
        var total = await q.CountAsync(ct);
        var items = await q
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return PagedResult<UserDto>.Create(
            items.Select(u => u.ToDto()).ToList(), total, query.PageNumber, query.PageSize);
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct) ?? throw new NotFoundException("User", id);
        return user.ToDto();
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _users.Query(tracking: true).FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("User", id);

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.IsActive = request.IsActive;

        await _uow.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task DeleteAsync(Guid id, Guid currentUserId, CancellationToken ct = default)
    {
        if (id == currentUserId)
            throw new BusinessRuleException("You cannot delete your own account.");

        var user = await _users.Query(tracking: true).FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("User", id);

        // Soft-deactivate rather than hard delete to preserve task/comment history.
        user.IsActive = false;
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<UserDto> AssignRoleAsync(Guid id, UserRole role, CancellationToken ct = default)
    {
        var user = await _users.Query(tracking: true).FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("User", id);

        user.Role = role;
        user.RefreshToken = null; // invalidate existing sessions so new permissions take effect
        user.RefreshTokenExpiry = null;
        await _uow.SaveChangesAsync(ct);
        return user.ToDto();
    }
}
