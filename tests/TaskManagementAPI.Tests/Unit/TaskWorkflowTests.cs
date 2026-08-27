using TaskManagementAPI.Domain;
using Xunit;

namespace TaskManagementAPI.Tests.Unit;

public class TaskWorkflowTests
{
    [Theory]
    [InlineData(WorkItemStatus.ToDo, WorkItemStatus.InProgress, true)]
    [InlineData(WorkItemStatus.ToDo, WorkItemStatus.Done, false)]
    [InlineData(WorkItemStatus.ToDo, WorkItemStatus.InReview, false)]
    [InlineData(WorkItemStatus.InProgress, WorkItemStatus.InReview, true)]
    [InlineData(WorkItemStatus.InProgress, WorkItemStatus.ToDo, true)]
    [InlineData(WorkItemStatus.InReview, WorkItemStatus.Done, true)]
    [InlineData(WorkItemStatus.InReview, WorkItemStatus.ToDo, false)]
    [InlineData(WorkItemStatus.Done, WorkItemStatus.InReview, true)]
    [InlineData(WorkItemStatus.Done, WorkItemStatus.ToDo, false)]
    [InlineData(WorkItemStatus.InProgress, WorkItemStatus.InProgress, true)]
    public void CanTransition_matches_state_machine(WorkItemStatus from, WorkItemStatus to, bool expected)
        => Assert.Equal(expected, TaskWorkflow.CanTransition(from, to));

    [Theory]
    [InlineData(TaskPriority.Low, TaskPriority.Medium)]
    [InlineData(TaskPriority.Medium, TaskPriority.High)]
    [InlineData(TaskPriority.High, TaskPriority.Critical)]
    [InlineData(TaskPriority.Critical, TaskPriority.Critical)]
    public void Escalate_bumps_one_level_capped_at_critical(TaskPriority input, TaskPriority expected)
        => Assert.Equal(expected, TaskWorkflow.Escalate(input));
}
