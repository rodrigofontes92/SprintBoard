using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SprintBoard.Api.Models;

namespace SprintBoard.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<WorkspaceProject> Projects => Set<WorkspaceProject>();

    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<WorkspaceProject>(entity =>
        {
            entity.Property(project => project.Name).HasMaxLength(120).IsRequired();
            entity.Property(project => project.Description).HasMaxLength(2_000);
            entity.Property(project => project.UpdatedAt).IsRequired();
            entity.HasIndex(project => new { project.OwnerId, project.Name }).IsUnique();
            entity.HasOne(project => project.Owner)
                .WithMany()
                .HasForeignKey(project => project.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProjectTask>(entity =>
        {
            entity.ToTable("ProjectTasks", table =>
            {
                table.HasCheckConstraint(
                    "CK_ProjectTasks_Status",
                    "\"Status\" IN ('Todo', 'InProgress', 'Done')");
                table.HasCheckConstraint(
                    "CK_ProjectTasks_Priority",
                    "\"Priority\" IN ('Low', 'Medium', 'High')");
            });
            entity.Property(task => task.Title).HasMaxLength(160).IsRequired();
            entity.Property(task => task.Description).HasMaxLength(2_000);
            entity.Property(task => task.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(task => task.Priority).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(task => task.DueDate).IsRequired(false);
            entity.Property(task => task.CreatedAt).IsRequired();
            entity.Property(task => task.UpdatedAt).IsRequired();
            entity.HasIndex(task => new { task.ProjectId, task.Status, task.DueDate });
            entity.HasOne(task => task.Project)
                .WithMany(project => project.Tasks)
                .HasForeignKey(task => task.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
