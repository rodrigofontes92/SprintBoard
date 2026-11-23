namespace SprintBoard.Api.Models;

public enum ProjectTaskStatus
{
    Todo,
    InProgress,
    Done
}

public enum TaskPriority
{
    Low,
    Medium,
    High
}

public sealed class ProjectTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public WorkspaceProject Project { get; set; } = null!;

    public required string Title { get; set; }

    public string? Description { get; set; }

    public ProjectTaskStatus Status { get; private set; } = ProjectTaskStatus.Todo;

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public DateOnly? DueDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool TryTransitionTo(ProjectTaskStatus nextStatus)
    {
        if (Status == nextStatus)
        {
            return true;
        }

        var isAllowed = Status switch
        {
            ProjectTaskStatus.Todo => nextStatus == ProjectTaskStatus.InProgress,
            ProjectTaskStatus.InProgress => nextStatus is ProjectTaskStatus.Todo or ProjectTaskStatus.Done,
            ProjectTaskStatus.Done => nextStatus == ProjectTaskStatus.InProgress,
            _ => false
        };

        if (isAllowed)
        {
            Status = nextStatus;
        }

        return isAllowed;
    }
}
