using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Security;
using TaskManagementAPI.Services;

namespace TaskManagementAPI.Controllers;

[ApiController]
[Route("api/comments")]
[Authorize]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _comments;

    public CommentsController(ICommentService comments) => _comments = comments;

    [HttpPut("{id:guid}")]
    [Authorize(Permissions.CommentsEdit)]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CommentDto>> Update(Guid id, UpdateCommentRequest request, CancellationToken ct)
        => Ok(await _comments.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Permissions.CommentsDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _comments.DeleteAsync(id, ct);
        return NoContent();
    }
}
