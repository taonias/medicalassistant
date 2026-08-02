using MedicalAssistant.Identity.DatabaseContext;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.AcceptanceTests;

/// <summary>
/// Models the deployment migration job for the in-process acceptance backend.
/// Application startup remains unchanged and does not own acceptance setup.
/// </summary>
internal static class BackendDatabaseMigrator
{
    public static async Task MigrateAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var persistenceOptions = new DbContextOptionsBuilder<MedicalAssistantDatabaseContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using (var persistence = new MedicalAssistantDatabaseContext(
                         persistenceOptions,
                         new HttpContextAccessor()))
        {
            await persistence.Database.MigrateAsync(cancellationToken);
        }

        var identityOptions = new DbContextOptionsBuilder<MedicalAssistantIdentityDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var identity = new MedicalAssistantIdentityDbContext(identityOptions);
        await identity.Database.MigrateAsync(cancellationToken);
    }
}
