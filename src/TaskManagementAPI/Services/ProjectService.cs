using Microsoft.EntityFrameworkCore;
using TaskManagementAPI.Common;
using TaskManagementAPI.Contracts;
using TaskManagementAPI.Domain;
using TaskManagementAPI.Repositories;

namespace TaskManagementAPI.Services;

public interface IProjectService
{
    Task<PagedResult<ProjectDto>> GetAsync(ProjectQuery query, CancellationToken ct = default);
    Task<ProjectDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDto> CreateAsync(CreateProjectRequest request, Guid currentUserId, CancellationToken ct = default);
    Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ProjectStatisticsDto> GetStatisticsAsync(Guid id, CancellationToken ct = default);
}

public class ProjectService : IProjectService
{
    private readonly IRepository<Project> _projects;
    private readonly IRepository<TaskItem> _tasks;
    private readonly IUnitOfWork _uow;

    public ProjectService(IRepository<Project> projects, IRepository<TaskItem> tasks, IUnitOfWork uow)
    {
        _projects = projects;
        _tasks = tasks;
        _uow = uow;
    }

    public async Task<PagedResult<ProjectDto>> GetAsync(ProjectQuery query, CancellationToken ct = default)
    {
        var q = _projects.Query().Include(p => p.Creator).Include(p => p.Tasks).AsQueryable();

        if (query.Status is not null)
            q = q.Where(p => p.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(p => p.Name.ToLower().Contains(term) ||
                             (p.Description != null && p.Description.ToLower().Contains(term)));
        }

        q = q.OrderByDescending(p => p.CreatedAt);

        var total = await q.CountAsync(ct);
        var items = await q
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return PagedResult<ProjectDto>.Create(
            items.Select(p => p.ToDto()).ToList(), total, query.PageNumber, query.PageSize);
    }

    public async Task<ProjectDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _projects.Query()
            .Include(p => p.Creator)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project", id);

        return project.ToDto();
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Status = ProjectStatus.Active,
            CreatedBy = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        await _projects.AddAsync(project, ct);
        await _uow.SaveChangesAsync(ct);
        return project.ToDto();
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var project = await _projects.Query(tracking: true).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project", id);

        if (project.Status == ProjectStatus.Archived)
            throw new BusinessRuleException("Archived projects cannot be modified.");

        project.Name = request.Name.Trim();
        project.Description = request.Description?.Trim();

        if (project.Status != request.Status)
        {
            project.Status = request.Status;
            project.CompletedAt = request.Status == ProjectStatus.Completed ? DateTime.UtcNow : null;
        }

        await _uow.SaveChangesAsync(ct);
        return project.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _projects.Query(tracking: true).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project", id);

        var hasActiveTasks = await _tasks.Query()
            .AnyAsync(t => t.ProjectId == id && t.Status != WorkItemStatus.Done, ct);

        if (hasActiveTasks)
            throw new BusinessRuleException("The project cannot be deleted while it has active (not Done) tasks.");

        _projects.Remove(project);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<ProjectStatisticsDto> GetStatisticsAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _projects.Query().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project", id);

        var tasks = await _tasks.Query().Where(t => t.ProjectId == id).ToListAsync(ct);
        var completed = tasks.Count(t => t.Status == WorkItemStatus.Done);

        return new ProjectStatisticsDto
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            TotalTasks = tasks.Count,
            CompletedTasks = completed,
            OverdueTasks = tasks.Count(t => t.IsOverdue),
            CompletionRate = tasks.Count == 0 ? 0 : Math.Round((double)completed / tasks.Count * 100, 1),
            TasksByStatus = tasks.GroupBy(t => t.Status.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            TasksByPriority = tasks.GroupBy(t => t.Priority.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            TotalEstimatedHours = tasks.Sum(t => t.EstimatedHours),
            TotalActualHours = tasks.Sum(t => t.ActualHours)
        };
    }
}
