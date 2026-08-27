using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Security;
using TaskManagementAPI.Services;

namespace TaskManagementAPI.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projects;
    private readonly ITaskService _tasks;
    private readonly ICurrentUser _currentUser;

    public ProjectsController(IProjectService projects, ITaskService tasks, ICurrentUser currentUser)
    {
        _projects = projects;
        _tasks = tasks;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Permissions.ProjectsView)]
    [ProducesResponseType(typeof(PagedResult<ProjectDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProjectDto>>> Get([FromQuery] ProjectQuery query, CancellationToken ct)
        => Ok(await _projects.GetAsync(query, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Permissions.ProjectsView)]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _projects.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Permissions.ProjectsCreate)]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var project = await _projects.CreateAsync(request, _currentUser.Id, ct);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Permissions.ProjectsEdit)]
    public async Task<ActionResult<ProjectDto>> Update(Guid id, UpdateProjectRequest request, CancellationToken ct)
        => Ok(await _projects.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Permissions.ProjectsDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _projects.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/tasks")]
    [Authorize(Permissions.TasksView)]
    [ProducesResponseType(typeof(PagedResult<TaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TaskDto>>> GetTasks(Guid id, [FromQuery] TaskQuery query, CancellationToken ct)
    {
        query.ProjectId = id;
        return Ok(await _tasks.GetAsync(query, ct));
    }

    [HttpGet("{id:guid}/statistics")]
    [Authorize(Permissions.ReportsView)]
    [ProducesResponseType(typeof(ProjectStatisticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectStatisticsDto>> GetStatistics(Guid id, CancellationToken ct)
        => Ok(await _projects.GetStatisticsAsync(id, ct));
}
