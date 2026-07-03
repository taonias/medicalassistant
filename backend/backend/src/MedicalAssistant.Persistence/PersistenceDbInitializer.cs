using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Persistence;

public static class PersistenceDbInitializer
{
    public static async Task MigrateAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MedicalAssistantDatabaseContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("MedicalAssistant.Persistence.PersistenceDbInitializer");

        await context.Database.MigrateAsync();
        logger.LogInformation("MedicalAssistant database migrated.");
    }
}
