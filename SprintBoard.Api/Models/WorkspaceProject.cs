namespace SprintBoard.Api.Models;

public sealed class WorkspaceProject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public string? Description { get; set; }

    public Guid OwnerId { get; set; }

    public AppUser Owner { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
