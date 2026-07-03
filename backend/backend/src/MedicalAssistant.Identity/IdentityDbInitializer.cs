using MedicalAssistant.Identity.DatabaseContext;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Identity;

public static class IdentityDbInitializer
{
    public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
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

        var identityContext = scope.ServiceProvider.GetRequiredService<MedicalAssistantIdentityDbContext>();
        await identityContext.Database.MigrateAsync();
    }
}
