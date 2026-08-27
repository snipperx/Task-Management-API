using System.ComponentModel.DataAnnotations;
using TaskManagementAPI.Common;
using TaskManagementAPI.Domain;

namespace TaskManagementAPI.Contracts;

public class TaskDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkItemStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public Guid ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? AssignedTo { get; set; }
    public string? AssigneeName { get; set; }
    public Guid CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal EstimatedHours { get; set; }
    public decimal ActualHours { get; set; }
    public bool IsOverdue { get; set; }
    public int CommentCount { get; set; }
}

public class CreateTaskRequest
{
    [Required, StringLength(100, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    public Guid ProjectId { get; set; }

    public Guid? AssignedTo { get; set; }

    [Required]
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    [FutureDate]
    public DateTime? DueDate { get; set; }

    [Range(0, 168)]
    public decimal EstimatedHours { get; set; }
}

public class UpdateTaskRequest
{
    [Required, StringLength(100, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [FutureDate]
    public DateTime? DueDate { get; set; }

    [Range(0, 168)]
    public decimal EstimatedHours { get; set; }

    [Range(0, 1000)]
    public decimal ActualHours { get; set; }
}

public class UpdateTaskStatusRequest
{
    [Required]
    public WorkItemStatus Status { get; set; }
}

public class AssignTaskRequest
{
    /// <summary>Target assignee. Null unassigns the task.</summary>
    public Guid? AssigneeId { get; set; }
}

public class UpdateTaskPriorityRequest
{
    [Required]
    public TaskPriority Priority { get; set; }
}

public class TaskQuery : PageQuery
{
    public WorkItemStatus? Status { get; set; }
    public TaskPriority? Priority { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? AssigneeId { get; set; }
    public bool? IsOverdue { get; set; }
    public string? Search { get; set; }
    /// <summary>One of: createdAt, dueDate, priority, title, status. Prefix with '-' for descending.</summary>
    public string? Sort { get; set; }
}

public class TaskStatisticsDto
{
    public int TotalTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int UnassignedTasks { get; set; }
    public double CompletionRate { get; set; }
    public Dictionary<string, int> TasksByStatus { get; set; } = new();
    public Dictionary<string, int> TasksByPriority { get; set; } = new();
}
