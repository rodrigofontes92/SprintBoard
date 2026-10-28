using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SprintBoard.Api.Models;

namespace SprintBoard.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<WorkspaceProject> Projects => Set<WorkspaceProject>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<WorkspaceProject>(entity =>
        {
            entity.Property(project => project.Name).HasMaxLength(120).IsRequired();
            entity.Property(project => project.Description).HasMaxLength(2_000);
            entity.HasIndex(project => new { project.OwnerId, project.Name }).IsUnique();
            entity.HasOne(project => project.Owner)
                .WithMany()
                .HasForeignKey(project => project.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
