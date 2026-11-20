using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SprintBoard.Api.Models;

namespace SprintBoard.Api.Data;

public static class DemoDataSeeder
{
    private const string DefaultEmail = "demo@sprintboard.local";

    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("DemoSeed:Enabled"))
        {
            return;
        }

        var password = configuration["DemoSeed:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Demo seeding is enabled, but 'DemoSeed:Password' is not configured. " +
                "Set it with .NET user-secrets; never commit a demo password.");
        }

        var email = string.IsNullOrWhiteSpace(configuration["DemoSeed:Email"])
            ? DefaultEmail
            : configuration["DemoSeed:Email"]!.Trim();
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Could not create the demo account: {errors}");
            }
        }

        var existingNames = await dbContext.Projects
            .Where(project => project.OwnerId == user.Id)
            .Select(project => project.Name)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var samples = new[]
        {
            new WorkspaceProject
            {
                Name = "SprintBoard demo",
                Description = "Sample project for exploring the SprintBoard workflow.",
                OwnerId = user.Id,
                CreatedAt = now,
                UpdatedAt = now
            },
            new WorkspaceProject
            {
                Name = "Product roadmap example",
                Description = "Fictional sample data. Replace it with your own projects.",
                OwnerId = user.Id,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        dbContext.Projects.AddRange(samples.Where(sample => !existingNames.Contains(sample.Name)));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
