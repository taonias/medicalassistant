using MedicalAssistant.Identity.DatabaseContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Identity;

public static class IdentityDbMigrator
{
    public static async Task MigrateAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MedicalAssistantIdentityDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("MedicalAssistant.Identity.IdentityDbMigrator");

        await context.Database.MigrateAsync();
        logger.LogInformation("MedicalAssistant identity database migrated.");
    }
}
