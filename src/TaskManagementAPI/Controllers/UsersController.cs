using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Security;
using TaskManagementAPI.Services;

namespace TaskManagementAPI.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;
    private readonly ICurrentUser _currentUser;

    public UsersController(IUserService users, ICurrentUser currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Permissions.UsersView)]
    [ProducesResponseType(typeof(PagedResult<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserDto>>> GetAll([FromQuery] PageQuery query, CancellationToken ct)
        => Ok(await _users.GetUsersAsync(query, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Permissions.UsersView)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _users.GetByIdAsync(id, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Permissions.UsersManage)]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest request, CancellationToken ct)
        => Ok(await _users.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Permissions.UsersManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _users.DeleteAsync(id, _currentUser.Id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/roles")]
    [Authorize(Permissions.UsersManage)]
    public async Task<ActionResult<UserDto>> AssignRole(Guid id, AssignRoleRequest request, CancellationToken ct)
        => Ok(await _users.AssignRoleAsync(id, request.Role, ct));
}
