using MedicalAssistant.Application.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace MedicalAssistant.Persistence.DatabaseContext;

/// <summary>
/// Design-time only: lets EF Core tooling (migrations add / script) construct the
/// context without booting the application host. It reads the same configuration the
/// app does — <c>Database:Provider</c> and <c>ConnectionStrings:*</c> from the API
/// project's appsettings plus environment variables — via the shared resolver, so no
/// connection string is hardcoded here. Never used at runtime.
/// </summary>
public sealed class MedicalAssistantDatabaseContextFactory
    : IDesignTimeDbContextFactory<MedicalAssistantDatabaseContext>
{
    public MedicalAssistantDatabaseContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();

        var provider = RelationalDatabaseProviderParser.FromConfiguration(configuration);
        var connectionString = RelationalDatabaseConnectionStringResolver.Resolve(configuration, provider);
        var migrationsAssembly = typeof(MedicalAssistantDatabaseContext).Assembly.FullName;

        var options = new DbContextOptionsBuilder<MedicalAssistantDatabaseContext>();
        switch (provider)
        {
            case RelationalDatabaseProvider.SqlServer:
                options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(migrationsAssembly));
                break;
            default:
                options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(migrationsAssembly));
                break;
        }

        return new MedicalAssistantDatabaseContext(options.Options, new HttpContextAccessor());
    }

    private static IConfiguration BuildConfiguration()
    {
        var environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        return new ConfigurationBuilder()
            .SetBasePath(ResolveApiConfigBasePath())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    // The connection string lives in the API project's appsettings. Design-time tooling may run
    // from the API directory (cwd already holds it) or the Persistence directory, so find the API
    // project by walking up to the solution — the same appsettings is used either way. Falls back
    // to environment variables when it cannot be located.
    private static string ResolveApiConfigBasePath()
    {
        const string apiProjectRelative = "backend/src/MedicalAssistant.Api";

        var current = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(current, "appsettings.json")))
        {
            return current;
        }

        foreach (var start in new[] { current, AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, apiProjectRelative);
                if (File.Exists(Path.Combine(candidate, "appsettings.json")))
                {
                    return candidate;
                }
            }
        }

        return current;
    }
}
