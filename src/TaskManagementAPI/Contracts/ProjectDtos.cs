using System.ComponentModel.DataAnnotations;
using TaskManagementAPI.Common;
using TaskManagementAPI.Domain;

namespace TaskManagementAPI.Contracts;

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; }
    public Guid CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TaskCount { get; set; }
}

public class CreateProjectRequest
{
    [Required, StringLength(150, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }
}

public class UpdateProjectRequest
{
    [Required, StringLength(150, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public ProjectStatus Status { get; set; }
}

public class ProjectQuery : PageQuery
{
    public ProjectStatus? Status { get; set; }
    public string? Search { get; set; }
}

public class ProjectStatisticsDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int OverdueTasks { get; set; }
    public double CompletionRate { get; set; }
    public Dictionary<string, int> TasksByStatus { get; set; } = new();
    public Dictionary<string, int> TasksByPriority { get; set; } = new();
    public decimal TotalEstimatedHours { get; set; }
    public decimal TotalActualHours { get; set; }
}
