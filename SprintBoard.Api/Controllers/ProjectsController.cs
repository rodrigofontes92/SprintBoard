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
            .Select(project => new ProjectResponse(project.Id, project.Name, project.Description, project.CreatedAt))
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
            .Select(item => new ProjectResponse(item.Id, item.Name, item.Description, item.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        return project is null ? NotFound() : Ok(project);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ProjectResponse>> CreateProject(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = new WorkspaceProject
        {
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            OwnerId = GetOwnerId()
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

        var response = new ProjectResponse(project.Id, project.Name, project.Description, project.CreatedAt);
        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, response);
    }

    private Guid GetOwnerId() => Guid.Parse(userManager.GetUserId(User)!);
}

public sealed record CreateProjectRequest(
    [Required, RegularExpression(@".*\S.*"), StringLength(120, MinimumLength = 1)] string Name,
    [StringLength(2_000)] string? Description);

public sealed record ProjectResponse(Guid Id, string Name, string? Description, DateTimeOffset CreatedAt);
