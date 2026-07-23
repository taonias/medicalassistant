using Microsoft.Extensions.Configuration;

namespace MedicalAssistant.Application.Configuration;

public static class RelationalDatabaseConnectionStringResolver
{
    public const string LegacyConnectionStringName = "MedicalAssistantDatabaseConnectionString";
    public const string PostgreSqlConnectionStringName = "MedicalAssistantDatabasePostgreSQL";
    public const string SqlServerConnectionStringName = "MedicalAssistantDatabaseSqlServer";

    public static string Resolve(IConfiguration configuration, RelationalDatabaseProvider provider)
    {
        var specificName = provider switch
        {
            RelationalDatabaseProvider.PostgreSql => PostgreSqlConnectionStringName,
            RelationalDatabaseProvider.SqlServer => SqlServerConnectionStringName,
            _ => PostgreSqlConnectionStringName
        };

        var specific = configuration.GetConnectionString(specificName);
        if (!string.IsNullOrWhiteSpace(specific))
            return specific;

        var legacy = configuration.GetConnectionString(LegacyConnectionStringName);
        if (!string.IsNullOrWhiteSpace(legacy))
            return legacy;

        throw new InvalidOperationException(
            $"No connection string configured for {provider}. Set ConnectionStrings:{specificName}, " +
            $"or set ConnectionStrings:{LegacyConnectionStringName} as a fallback.");
    }
}
