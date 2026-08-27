namespace TaskManagementAPI.Domain;

/// <summary>
/// Encapsulates the task status state-machine:
/// ToDo → InProgress → InReview → Done, with single-step moves backward allowed.
/// Skipping states (e.g. ToDo → Done) is rejected.
/// </summary>
public static class TaskWorkflow
{
    private static readonly IReadOnlyDictionary<WorkItemStatus, WorkItemStatus[]> Allowed =
        new Dictionary<WorkItemStatus, WorkItemStatus[]>
        {
            [WorkItemStatus.ToDo] = new[] { WorkItemStatus.InProgress },
            [WorkItemStatus.InProgress] = new[] { WorkItemStatus.ToDo, WorkItemStatus.InReview },
            [WorkItemStatus.InReview] = new[] { WorkItemStatus.InProgress, WorkItemStatus.Done },
            [WorkItemStatus.Done] = new[] { WorkItemStatus.InReview }
        };

    public static bool CanTransition(WorkItemStatus from, WorkItemStatus to)
        => from == to || (Allowed.TryGetValue(from, out var targets) && targets.Contains(to));

    public static TaskPriority Escalate(TaskPriority current)
        => current == TaskPriority.Critical ? current : current + 1;
}
