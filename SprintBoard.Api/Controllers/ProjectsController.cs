using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SprintBoard.Api.Data;
using SprintBoard.Api.Models;

namespace SprintBoard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects")]
public sealed class ProjectsController(
    ApplicationDbContext dbContext,
    UserManager<AppUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> GetProjects(CancellationToken cancellationToken)
    {
        var ownerId = GetOwnerId();
        var projects = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.OwnerId == ownerId)
            .OrderBy(project => project.Name)
            .Select(project => new ProjectResponse(
                project.Id, project.Name, project.Description, project.CreatedAt, project.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(projects);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectResponse>> GetProject(Guid id, CancellationToken cancellationToken)
    {
        var ownerId = GetOwnerId();
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(item => item.Id == id && item.OwnerId == ownerId)
            .Select(item => new ProjectResponse(
                item.Id, item.Name, item.Description, item.CreatedAt, item.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        return project is null ? NotFound() : Ok(project);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ProjectResponse>> CreateProject(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var ownerId = GetOwnerId();
        var name = request.Name.Trim();
        if (await dbContext.Projects.AnyAsync(
            project => project.OwnerId == ownerId && project.Name == name,
            cancellationToken))
        {
            return Conflict(new { error = "A project with this name already exists in your account." });
        }

        var now = DateTimeOffset.UtcNow;
        var project = new WorkspaceProject
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            OwnerId = ownerId,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Projects.Add(project);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            return Conflict(new { error = "A project with this name already exists in your account." });
        }

        var response = ToResponse(project);
        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ProjectResponse>> UpdateProject(
        Guid id,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var ownerId = GetOwnerId();
        var project = await dbContext.Projects
            .SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        var name = request.Name.Trim();
        if (await dbContext.Projects.AnyAsync(
            item => item.OwnerId == ownerId && item.Id != id && item.Name == name,
            cancellationToken))
        {
            return Conflict(new { error = "A project with this name already exists in your account." });
        }

        project.Name = name;
        project.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        var now = DateTimeOffset.UtcNow;
        project.UpdatedAt = now > project.UpdatedAt ? now : project.UpdatedAt.AddTicks(1);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        })
        {
            return Conflict(new { error = "A project with this name already exists in your account." });
        }

        return Ok(ToResponse(project));
    }

    [HttpDelete("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProject(Guid id, CancellationToken cancellationToken)
    {
        var ownerId = GetOwnerId();
        var project = await dbContext.Projects
            .SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        dbContext.Projects.Remove(project);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Guid GetOwnerId() => Guid.Parse(userManager.GetUserId(User)!);

    private static ProjectResponse ToResponse(WorkspaceProject project) =>
        new(project.Id, project.Name, project.Description, project.CreatedAt, project.UpdatedAt);
}

public sealed record CreateProjectRequest(
    [Required, RegularExpression(@".*\S.*"), StringLength(120, MinimumLength = 1)] string Name,
    [StringLength(2_000)] string? Description);

public sealed record UpdateProjectRequest(
    [Required, RegularExpression(@".*\S.*"), StringLength(120, MinimumLength = 1)] string Name,
    [StringLength(2_000)] string? Description);

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
