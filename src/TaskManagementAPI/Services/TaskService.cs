using Microsoft.EntityFrameworkCore;
using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Domain;
using TaskManagementAPI.Repositories;
using TaskManagementAPI.Security;

namespace TaskManagementAPI.Services;

public interface ITaskService
{
    Task<PagedResult<TaskDto>> GetAsync(TaskQuery query, CancellationToken ct = default);
    Task<TaskDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TaskDto> CreateAsync(CreateTaskRequest request, CancellationToken ct = default);
    Task<TaskDto> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<TaskDto> ChangeStatusAsync(Guid id, WorkItemStatus status, CancellationToken ct = default);
    Task<TaskDto> AssignAsync(Guid id, Guid? assigneeId, CancellationToken ct = default);
    Task<TaskDto> ChangePriorityAsync(Guid id, TaskPriority priority, CancellationToken ct = default);
    Task<IReadOnlyList<TaskDto>> GetOverdueAsync(CancellationToken ct = default);
    Task<TaskStatisticsDto> GetStatisticsAsync(Guid? projectId, CancellationToken ct = default);
}

public class TaskService : ITaskService
{
    public const int MaxInProgressPerUser = 10;

    private readonly IRepository<TaskItem> _tasks;
    private readonly IRepository<Project> _projects;
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<TaskService> _logger;

    public TaskService(
        IRepository<TaskItem> tasks,
        IRepository<Project> projects,
        IRepository<User> users,
        IUnitOfWork uow,
        ICurrentUser currentUser,
        ILogger<TaskService> logger)
    {
        _tasks = tasks;
        _projects = projects;
        _users = users;
        _uow = uow;
        _currentUser = currentUser;
        _logger = logger;
    }

    private IQueryable<TaskItem> WithGraph(bool tracking = false) => _tasks.Query(tracking)
        .Include(t => t.Project)
        .Include(t => t.Assignee)
        .Include(t => t.Creator)
        .Include(t => t.Comments);

    public async Task<PagedResult<TaskDto>> GetAsync(TaskQuery query, CancellationToken ct = default)
    {
        var q = WithGraph();

        if (query.Status is not null) q = q.Where(t => t.Status == query.Status);
        if (query.Priority is not null) q = q.Where(t => t.Priority == query.Priority);
        if (query.ProjectId is not null) q = q.Where(t => t.ProjectId == query.ProjectId);
        if (query.AssigneeId is not null) q = q.Where(t => t.AssignedTo == query.AssigneeId);

        if (query.IsOverdue == true)
            q = q.Where(t => t.DueDate != null && t.Status != WorkItemStatus.Done && t.DueDate < DateTime.UtcNow.Date);
        else if (query.IsOverdue == false)
            q = q.Where(t => t.DueDate == null || t.Status == WorkItemStatus.Done || t.DueDate >= DateTime.UtcNow.Date);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(t => t.Title.ToLower().Contains(term) ||
                             (t.Description != null && t.Description.ToLower().Contains(term)));
        }

        q = ApplySort(q, query.Sort);

        var total = await q.CountAsync(ct);
        var items = await q
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return PagedResult<TaskDto>.Create(
            items.Select(t => t.ToDto()).ToList(), total, query.PageNumber, query.PageSize);
    }

    private static IQueryable<TaskItem> ApplySort(IQueryable<TaskItem> q, string? sort)
    {
        var desc = sort?.StartsWith('-') == true;
        var field = sort?.TrimStart('-').ToLowerInvariant();

        return field switch
        {
            "duedate" => desc ? q.OrderByDescending(t => t.DueDate) : q.OrderBy(t => t.DueDate),
            "priority" => desc ? q.OrderByDescending(t => t.Priority) : q.OrderBy(t => t.Priority),
            "title" => desc ? q.OrderByDescending(t => t.Title) : q.OrderBy(t => t.Title),
            "status" => desc ? q.OrderByDescending(t => t.Status) : q.OrderBy(t => t.Status),
            _ => desc ? q.OrderBy(t => t.CreatedAt) : q.OrderByDescending(t => t.CreatedAt)
        };
    }

    public async Task<TaskDto> GetByIdAsync(Guid id, CancellationToken ct = default)
        => (await LoadAsync(id, tracking: false, ct)).ToDto();

    public async Task<TaskDto> CreateAsync(CreateTaskRequest request, CancellationToken ct = default)
    {
        var project = await _projects.Query().FirstOrDefaultAsync(p => p.Id == request.ProjectId, ct)
            ?? throw new NotFoundException("Project", request.ProjectId);

        if (project.Status != ProjectStatus.Active)
            throw new BusinessRuleException("Tasks can only be created in Active projects.");

        if (request.DueDate is { } due && due.Date < DateTime.UtcNow.Date)
            throw new ValidationException("Due date cannot be in the past.");

        if (request.AssignedTo is { } assigneeId)
            await EnsureAssigneeExistsAsync(assigneeId, ct);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Status = WorkItemStatus.ToDo,
            Priority = request.Priority,
            ProjectId = request.ProjectId,
            AssignedTo = request.AssignedTo,
            CreatedBy = _currentUser.Id,
            CreatedAt = DateTime.UtcNow,
            DueDate = request.DueDate,
            EstimatedHours = request.EstimatedHours
        };

        await _tasks.AddAsync(task, ct);
        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Task {TaskId} created in project {ProjectId}", task.Id, task.ProjectId);

        return (await LoadAsync(task.Id, tracking: false, ct)).ToDto();
    }

    public async Task<TaskDto> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var task = await LoadAsync(id, tracking: true, ct);
        EnsureCanModify(task);

        if (request.DueDate is { } due && due.Date < DateTime.UtcNow.Date && due != task.DueDate)
            throw new ValidationException("Due date cannot be in the past.");

        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.DueDate = request.DueDate;
        task.EstimatedHours = request.EstimatedHours;
        task.ActualHours = request.ActualHours;

        await _uow.SaveChangesAsync(ct);
        return task.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var task = await LoadAsync(id, tracking: true, ct);
        task.IsDeleted = true; // soft delete
        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Task {TaskId} soft-deleted", id);
    }

    public async Task<TaskDto> ChangeStatusAsync(Guid id, WorkItemStatus status, CancellationToken ct = default)
    {
        var task = await LoadAsync(id, tracking: true, ct);
        EnsureCanModify(task);

        if (!TaskWorkflow.CanTransition(task.Status, status))
            throw new BusinessRuleException(
                $"Cannot move a task from {task.Status} to {status}. Allowed flow: ToDo → InProgress → InReview → Done.");

        if (status == WorkItemStatus.InProgress && task.Status != WorkItemStatus.InProgress)
            await EnsureInProgressCapacityAsync(task.AssignedTo ?? _currentUser.Id, ct);

        task.Status = status;
        task.CompletedAt = status == WorkItemStatus.Done ? DateTime.UtcNow : null;

        await _uow.SaveChangesAsync(ct);
        return task.ToDto();
    }

    public async Task<TaskDto> AssignAsync(Guid id, Guid? assigneeId, CancellationToken ct = default)
    {
        var task = await LoadAsync(id, tracking: true, ct);

        if (assigneeId is { } aid)
        {
            await EnsureAssigneeExistsAsync(aid, ct);
            if (task.Status == WorkItemStatus.InProgress && task.AssignedTo != aid)
                await EnsureInProgressCapacityAsync(aid, ct);
        }

        task.AssignedTo = assigneeId;
        await _uow.SaveChangesAsync(ct);
        return task.ToDto();
    }

    public async Task<TaskDto> ChangePriorityAsync(Guid id, TaskPriority priority, CancellationToken ct = default)
    {
        var task = await LoadAsync(id, tracking: true, ct);
        EnsureCanModify(task);
        task.Priority = priority;
        await _uow.SaveChangesAsync(ct);
        return task.ToDto();
    }

    public async Task<IReadOnlyList<TaskDto>> GetOverdueAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var overdue = await WithGraph()
            .Where(t => t.DueDate != null && t.Status != WorkItemStatus.Done && t.DueDate < today)
            .OrderBy(t => t.DueDate)
            .ToListAsync(ct);

        return overdue.Select(t => t.ToDto()).ToList();
    }

    public async Task<TaskStatisticsDto> GetStatisticsAsync(Guid? projectId, CancellationToken ct = default)
    {
        var q = _tasks.Query();
        if (projectId is not null) q = q.Where(t => t.ProjectId == projectId);

        var tasks = await q.ToListAsync(ct);
        var completed = tasks.Count(t => t.Status == WorkItemStatus.Done);

        return new TaskStatisticsDto
        {
            TotalTasks = tasks.Count,
            OverdueTasks = tasks.Count(t => t.IsOverdue),
            CompletedTasks = completed,
            UnassignedTasks = tasks.Count(t => t.AssignedTo is null),
            CompletionRate = tasks.Count == 0 ? 0 : Math.Round((double)completed / tasks.Count * 100, 1),
            TasksByStatus = tasks.GroupBy(t => t.Status.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            TasksByPriority = tasks.GroupBy(t => t.Priority.ToString()).ToDictionary(g => g.Key, g => g.Count())
        };
    }

    // ---- helpers -------------------------------------------------------------

    private async Task<TaskItem> LoadAsync(Guid id, bool tracking, CancellationToken ct)
        => await WithGraph(tracking).FirstOrDefaultAsync(t => t.Id == id, ct)
           ?? throw new NotFoundException("Task", id);

    /// <summary>Developers/Viewers may only modify tasks assigned to them; Admins/Managers may modify any.</summary>
    private void EnsureCanModify(TaskItem task)
    {
        if (_currentUser.IsAdminOrManager) return;
        if (task.AssignedTo == _currentUser.Id) return;
        throw new ForbiddenException("You can only modify tasks assigned to you.");
    }

    private async Task EnsureAssigneeExistsAsync(Guid assigneeId, CancellationToken ct)
    {
        var ok = await _users.AnyAsync(u => u.Id == assigneeId && u.IsActive, ct);
        if (!ok) throw new ValidationException($"Assignee '{assigneeId}' does not exist or is inactive.");
    }

    private async Task EnsureInProgressCapacityAsync(Guid userId, CancellationToken ct)
    {
        var count = await _tasks.Query()
            .CountAsync(t => t.AssignedTo == userId && t.Status == WorkItemStatus.InProgress, ct);

        if (count >= MaxInProgressPerUser)
            throw new BusinessRuleException(
                $"A user may have at most {MaxInProgressPerUser} tasks In Progress at once.");
    }
}
