namespace TaskManagementAPI.Domain;

public enum UserRole
{
    Viewer = 0,
    Developer = 1,
    Manager = 2,
    Admin = 3
}

public enum ProjectStatus
{
    Active = 0,
    Completed = 1,
    Archived = 2
}

/// <summary>
/// Workflow state of a task. Named WorkItemStatus to avoid colliding with
/// <see cref="System.Threading.Tasks.TaskStatus"/> which is in scope via ImplicitUsings.
/// </summary>
public enum WorkItemStatus
{
    ToDo = 0,
    InProgress = 1,
    InReview = 2,
    Done = 3
}

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
