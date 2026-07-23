using Microsoft.Extensions.Configuration;

namespace MedicalAssistant.Application.Configuration;

public enum RelationalDatabaseProvider
{
    PostgreSql = 0,
    SqlServer = 1
}

public static class RelationalDatabaseProviderParser
{
    public const string ConfigurationKey = "Database:Provider";

    public static RelationalDatabaseProvider FromConfiguration(IConfiguration configuration)
    {
        var raw = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(raw))
            return RelationalDatabaseProvider.PostgreSql;

        if (string.Equals(raw, "SqlServer", StringComparison.OrdinalIgnoreCase))
            return RelationalDatabaseProvider.SqlServer;

        if (string.Equals(raw, "PostgreSQL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "PostgreSql", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "Postgres", StringComparison.OrdinalIgnoreCase))
            return RelationalDatabaseProvider.PostgreSql;

        throw new InvalidOperationException(
            $"Unsupported {ConfigurationKey} value '{raw}'. Use PostgreSQL, PostgreSql, Postgres, or SqlServer.");
    }
}
