using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SprintBoard.Api.Data;
using SprintBoard.Api.Models;

namespace SprintBoard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/tasks")]
public sealed class ProjectTasksController(
    ApplicationDbContext dbContext,
    UserManager<AppUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectTaskResponse>>> GetTasks(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var ownerId = GetOwnerId();
        var ownsProject = await dbContext.Projects.AnyAsync(
            project => project.Id == projectId && project.OwnerId == ownerId,
            cancellationToken);

        if (!ownsProject)
        {
            return NotFound();
        }

        var tasks = await dbContext.ProjectTasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId)
            .OrderBy(task => task.DueDate)
            .ThenBy(task => task.Title)
            .Select(task => new ProjectTaskResponse(
                task.Id,
                task.ProjectId,
                task.Title,
                task.Description,
                task.Status,
                task.Priority,
                task.DueDate,
                task.CreatedAt,
                task.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(tasks);
    }

    [HttpGet("{taskId:guid}")]
    public async Task<ActionResult<ProjectTaskResponse>> GetTask(
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var ownerId = GetOwnerId();
        var task = await dbContext.ProjectTasks
            .AsNoTracking()
            .Where(item => item.Id == taskId && item.ProjectId == projectId && item.Project.OwnerId == ownerId)
            .Select(item => new ProjectTaskResponse(
                item.Id,
                item.ProjectId,
                item.Title,
                item.Description,
                item.Status,
                item.Priority,
                item.DueDate,
                item.CreatedAt,
                item.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        return task is null ? NotFound() : Ok(task);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ProjectTaskResponse>> CreateTask(
        Guid projectId,
        CreateProjectTaskRequest request,
        CancellationToken cancellationToken)
    {
        var ownerId = GetOwnerId();
        var ownsProject = await dbContext.Projects.AnyAsync(
            project => project.Id == projectId && project.OwnerId == ownerId,
            cancellationToken);

        if (!ownsProject)
        {
            return NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var task = new ProjectTask
        {
            ProjectId = projectId,
            Title = request.Title.Trim(),
            Description = NormalizeDescription(request.Description),
            Priority = request.Priority!.Value,
            DueDate = request.DueDate,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ProjectTasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = ToResponse(task);
        return CreatedAtAction(nameof(GetTask), new { projectId, taskId = task.Id }, response);
    }

    [HttpPut("{taskId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ProjectTaskResponse>> UpdateTask(
        Guid projectId,
        Guid taskId,
        UpdateProjectTaskRequest request,
        CancellationToken cancellationToken)
    {
        var ownerId = GetOwnerId();
        var task = await dbContext.ProjectTasks.SingleOrDefaultAsync(
            item => item.Id == taskId && item.ProjectId == projectId && item.Project.OwnerId == ownerId,
            cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        var nextStatus = request.Status!.Value;
        if (!task.TryTransitionTo(nextStatus))
        {
            return BadRequest(new
            {
                error = $"The task cannot transition from '{task.Status}' to '{nextStatus}'."
            });
        }

        task.Title = request.Title.Trim();
        task.Description = NormalizeDescription(request.Description);
        task.Priority = request.Priority!.Value;
        task.DueDate = request.DueDate;
        var now = DateTimeOffset.UtcNow;
        task.UpdatedAt = now > task.UpdatedAt ? now : task.UpdatedAt.AddTicks(1);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(task));
    }

    [HttpDelete("{taskId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTask(
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var ownerId = GetOwnerId();
        var task = await dbContext.ProjectTasks.SingleOrDefaultAsync(
            item => item.Id == taskId && item.ProjectId == projectId && item.Project.OwnerId == ownerId,
            cancellationToken);

        if (task is null)
        {
            return NotFound();
        }

        dbContext.ProjectTasks.Remove(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Guid GetOwnerId() => Guid.Parse(userManager.GetUserId(User)!);

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static ProjectTaskResponse ToResponse(ProjectTask task) =>
        new(
            task.Id,
            task.ProjectId,
            task.Title,
            task.Description,
            task.Status,
            task.Priority,
            task.DueDate,
            task.CreatedAt,
            task.UpdatedAt);
}

public sealed record CreateProjectTaskRequest(
    [Required, RegularExpression(@".*\S.*"), StringLength(160, MinimumLength = 1)] string Title,
    [StringLength(2_000)] string? Description,
    [Required] TaskPriority? Priority,
    DateOnly? DueDate);

public sealed record UpdateProjectTaskRequest(
    [Required, RegularExpression(@".*\S.*"), StringLength(160, MinimumLength = 1)] string Title,
    [StringLength(2_000)] string? Description,
    [Required] ProjectTaskStatus? Status,
    [Required] TaskPriority? Priority,
    DateOnly? DueDate);

public sealed record ProjectTaskResponse(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    ProjectTaskStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
