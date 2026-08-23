using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;

namespace MedicalAssistant.Persistence.IntegrationTests;

public sealed class PostgreSqlDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg16")
        .WithDatabase("medical_assistant_persistence_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public MedicalAssistantDatabaseContext CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<MedicalAssistantDatabaseContext>()
            .UseNpgsql(_container.GetConnectionString());
        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }

        return new MedicalAssistantDatabaseContext(options.Options, new HttpContextAccessor());
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ResetAsync();
    }

    public async Task ResetAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlDatabaseCollection : ICollectionFixture<PostgreSqlDatabaseFixture>
{
    public const string Name = "PostgreSQL persistence";
}
