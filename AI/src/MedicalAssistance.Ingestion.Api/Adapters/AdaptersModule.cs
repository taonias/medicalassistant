using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Pgvector.Npgsql;

namespace MedicalAssistance.Ingestion.Api.Ingestions;

/// <summary>
/// Composition root for every external-system adapter (R26): Postgres/pgvector,
/// the AI provider seams (chat, embeddings, PDF extraction — placeholder until
/// configured), the document archive, and the RabbitMQ integration-event outbox.
///
/// Registration order within this method is load-bearing for the three provider
/// seams: each starts with an <c>Unconfigured*</c> placeholder via
/// <c>TryAddSingleton</c>, then <see cref="OpenAiProviders.AddOpenAiProviders"/>
/// and <see cref="AzureAi.AddAzureProviders"/> add the real clients only when
/// configured. .NET DI resolves the *last* registration for single-instance
/// <c>GetService&lt;T&gt;()</c>, so the placeholder must run first and Azure last —
/// a configured OpenAI client beats the placeholder, and a configured Azure
/// client — added last — wins when both are set.
/// </summary>
public static class AdaptersModule
{
    public static IServiceCollection AddAdapters(
        this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);
        services.AddDbContext<IngestionDbContext>(options =>
            options.UseNpgsql(dataSource, npgsql => npgsql.UseVector()));

        services.TryAddSingleton<IChatClient>(new UnconfiguredChatClient());
        services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(new UnconfiguredEmbeddingGenerator());

        // The extraction seam (ADR-0005): one provider-neutral interface for turning a
        // PDF into text + table cell grids. Unconfigured by default so the app boots with
        // no Azure account and fails loudly only if a PDF is actually processed; a real
        // Azure Document Intelligence adapter replaces it by configuration, a fake by DI.
        services.TryAddSingleton<IDocumentExtractor>(new UnconfiguredDocumentExtractor());

        // Real providers replace the placeholders above when their configuration is present
        // — provider choice is configuration, not architecture. Plain OpenAI (chat +
        // embedding) is registered first, then the Azure providers (chat + embedding;
        // ADR-0005 extraction). Order is deterministic: both beat the placeholder as later
        // registrations, and Azure — added last — wins when both a plain-OpenAI and an Azure
        // section are configured for the same seam.
        services.AddOpenAiProviders(configuration);
        services.AddAzureProviders(configuration);

        // The document archive: a local landing zone that saves each submitted document
        // to a filesystem folder structure before ingestion, active only when a root path
        // is configured (for local testing). Off by default — the database payload is the
        // system of record either way.
        var documentArchiveRoot = configuration.GetValue<string>("DocumentArchive:LocalRootPath");
        if (!string.IsNullOrWhiteSpace(documentArchiveRoot))
            services.AddSingleton<IIngestedDocumentArchive>(sp =>
                new LocalFileSystemDocumentArchive(
                    documentArchiveRoot, sp.GetRequiredService<ILogger<LocalFileSystemDocumentArchive>>()));
        else
            services.AddSingleton<IIngestedDocumentArchive, NullDocumentArchive>();

        // Publish integration events (a transcript ingestion failing) to the shared event bus via a
        // transactional outbox, so the backend can reconcile the consultation into a retryable failure.
        // Inert unless RabbitMQ:Host is configured, keeping the service standalone-capable.
        services.Configure<RabbitMqPublishOptions>(configuration.GetSection(RabbitMqPublishOptions.SectionName));
        services.AddSingleton<RabbitMqEventPublisher>();
        services.AddHostedService<IntegrationEventOutboxRelay>();

        return services;
    }
}
