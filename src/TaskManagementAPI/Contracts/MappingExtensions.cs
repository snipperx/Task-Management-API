using TaskManagementAPI.Domain;

namespace TaskManagementAPI.Contracts;

public static class MappingExtensions
{
    public static UserDto ToDto(this User u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        FirstName = u.FirstName,
        LastName = u.LastName,
        FullName = u.FullName,
        Role = u.Role,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt
    };

    public static ProjectDto ToDto(this Project p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Status = p.Status,
        CreatedBy = p.CreatedBy,
        CreatedByName = p.Creator?.FullName,
        CreatedAt = p.CreatedAt,
        CompletedAt = p.CompletedAt,
        TaskCount = p.Tasks?.Count ?? 0
    };

    public static TaskDto ToDto(this TaskItem t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Description = t.Description,
        Status = t.Status,
        Priority = t.Priority,
        ProjectId = t.ProjectId,
        ProjectName = t.Project?.Name,
        AssignedTo = t.AssignedTo,
        AssigneeName = t.Assignee?.FullName,
        CreatedBy = t.CreatedBy,
        CreatedByName = t.Creator?.FullName,
        CreatedAt = t.CreatedAt,
        DueDate = t.DueDate,
        CompletedAt = t.CompletedAt,
        EstimatedHours = t.EstimatedHours,
        ActualHours = t.ActualHours,
        IsOverdue = t.IsOverdue,
        CommentCount = t.Comments?.Count ?? 0
    };

    public static CommentDto ToDto(this TaskComment c) => new()
    {
        Id = c.Id,
        TaskId = c.TaskId,
        Content = c.Content,
        UserId = c.UserId,
        UserName = c.User?.FullName,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
