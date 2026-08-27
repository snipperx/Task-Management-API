using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Security;
using TaskManagementAPI.Services;

namespace TaskManagementAPI.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskService _tasks;

    public TasksController(ITaskService tasks) => _tasks = tasks;

    [HttpGet]
    [Authorize(Permissions.TasksView)]
    [ProducesResponseType(typeof(PagedResult<TaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TaskDto>>> Get([FromQuery] TaskQuery query, CancellationToken ct)
        => Ok(await _tasks.GetAsync(query, ct));

    [HttpGet("statistics")]
    [Authorize(Permissions.ReportsView)]
    [ProducesResponseType(typeof(TaskStatisticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskStatisticsDto>> Statistics([FromQuery] Guid? projectId, CancellationToken ct)
        => Ok(await _tasks.GetStatisticsAsync(projectId, ct));

    [HttpGet("overdue")]
    [Authorize(Permissions.TasksView)]
    [ProducesResponseType(typeof(IReadOnlyList<TaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> Overdue(CancellationToken ct)
        => Ok(await _tasks.GetOverdueAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Permissions.TasksView)]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _tasks.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Permissions.TasksCreate)]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskDto>> Create(CreateTaskRequest request, CancellationToken ct)
    {
        var task = await _tasks.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, task);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Permissions.TasksEdit)]
    public async Task<ActionResult<TaskDto>> Update(Guid id, UpdateTaskRequest request, CancellationToken ct)
        => Ok(await _tasks.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Permissions.TasksDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _tasks.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Permissions.TasksStatusUpdate)]
    public async Task<ActionResult<TaskDto>> ChangeStatus(Guid id, UpdateTaskStatusRequest request, CancellationToken ct)
        => Ok(await _tasks.ChangeStatusAsync(id, request.Status, ct));

    [HttpPatch("{id:guid}/assign")]
    [Authorize(Permissions.TasksAssign)]
    public async Task<ActionResult<TaskDto>> Assign(Guid id, AssignTaskRequest request, CancellationToken ct)
        => Ok(await _tasks.AssignAsync(id, request.AssigneeId, ct));

    [HttpPatch("{id:guid}/priority")]
    [Authorize(Permissions.TasksEdit)]
    public async Task<ActionResult<TaskDto>> ChangePriority(Guid id, UpdateTaskPriorityRequest request, CancellationToken ct)
        => Ok(await _tasks.ChangePriorityAsync(id, request.Priority, ct));

    // ----- nested comment routes -------------------------------------------------

    [HttpGet("{taskId:guid}/comments")]
    [Authorize(Permissions.TasksView)]
    [ProducesResponseType(typeof(IReadOnlyList<CommentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CommentDto>>> GetComments(
        Guid taskId, [FromServices] ICommentService comments, CancellationToken ct)
        => Ok(await comments.GetForTaskAsync(taskId, ct));

    [HttpPost("{taskId:guid}/comments")]
    [Authorize(Permissions.CommentsCreate)]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CommentDto>> AddComment(
        Guid taskId, CreateCommentRequest request, [FromServices] ICommentService comments, CancellationToken ct)
    {
        var comment = await comments.AddAsync(taskId, request, ct);
        return CreatedAtAction(nameof(GetComments), new { taskId }, comment);
    }
}
