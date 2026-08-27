namespace TaskManagementAPI.Domain;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Viewer;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    // Navigation
    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskItem> CreatedTasks { get; set; } = new List<TaskItem>();
    public ICollection<Project> CreatedProjects { get; set; } = new List<Project>();
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public User? Creator { get; set; }
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}

public class TaskItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkItemStatus Status { get; set; } = WorkItemStatus.ToDo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public Guid? AssignedTo { get; set; }
    public Guid ProjectId { get; set; }
    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }

    public decimal EstimatedHours { get; set; }
    public decimal ActualHours { get; set; }

    public bool IsDeleted { get; set; }

    // Navigation
    public User? Assignee { get; set; }
    public User? Creator { get; set; }
    public Project? Project { get; set; }
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();

    public bool IsOverdue =>
        DueDate.HasValue && Status != WorkItemStatus.Done && DueDate.Value.Date < DateTime.UtcNow.Date;
}

public class TaskComment
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public TaskItem? Task { get; set; }
    public User? User { get; set; }
}
