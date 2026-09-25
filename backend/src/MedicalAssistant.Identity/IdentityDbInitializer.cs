using MedicalAssistant.Application.Models;
using MedicalAssistant.Identity.DatabaseContext;
using MedicalAssistant.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Identity;

public static class IdentityDbInitializer
{
    public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var identityContext = scope.ServiceProvider.GetRequiredService<MedicalAssistantIdentityDbContext>();
        await identityContext.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("MedicalAssistant.Identity.IdentityDbInitializer");

        string[] roles = ["Doctor", "Administrator"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (result.Succeeded)
                    logger.LogInformation("Created role {Role}", role);
            }
        }

        await SeedAdminAsync(scope.ServiceProvider, logger);
    }

    private static async Task SeedAdminAsync(IServiceProvider services, ILogger logger)
    {
        var seed = services.GetRequiredService<IOptions<AdminSeedSettings>>().Value;
        if (string.IsNullOrWhiteSpace(seed.UserName)
            || string.IsNullOrWhiteSpace(seed.Email)
            || string.IsNullOrWhiteSpace(seed.Password))
        {
            logger.LogInformation("AdminSeed is not configured; skipping administrator user seed.");
            return;
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(seed.Email)
            ?? await userManager.FindByNameAsync(seed.UserName);

        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = seed.UserName.Trim(),
                Email = seed.Email.Trim(),
                FirstName = "Admin",
                LastName = "User",
                EmailConfirmed = true,
                IsApproved = true
            };

            var createResult = await userManager.CreateAsync(user, seed.Password);
            if (!createResult.Succeeded)
            {
                logger.LogError(
                    "Failed to seed administrator: {Errors}",
                    string.Join(' ', createResult.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Seeded administrator user {UserName}", user.UserName);
        }
        else if (!user.IsApproved)
        {
            user.IsApproved = true;
            await userManager.UpdateAsync(user);
        }

        await EnsureRoleAsync(userManager, user, "Doctor", logger);
        await EnsureRoleAsync(userManager, user, "Administrator", logger);
    }

    private static async Task EnsureRoleAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string role,
        ILogger logger)
    {
        if (await userManager.IsInRoleAsync(user, role))
            return;

        var result = await userManager.AddToRoleAsync(user, role);
        if (result.Succeeded)
            logger.LogInformation("Added role {Role} to user {UserName}", role, user.UserName);
        else
            logger.LogWarning(
                "Failed to add role {Role} to {UserName}: {Errors}",
                role,
                user.UserName,
                string.Join(' ', result.Errors.Select(e => e.Description)));
    }
}
