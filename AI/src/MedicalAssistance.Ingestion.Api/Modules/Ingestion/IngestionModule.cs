using System.Threading.Channels;
using MedicalAssistance.Ingestion.Api.Realtime;

namespace MedicalAssistance.Ingestion.Api.Ingestions;

/// <summary>
/// Composition root for the Ingestion capability (R26): the status publisher,
/// the store and queue, the strategy registry (ADR-0004), and the background
/// worker/recovery-sweep hosted services. Registered as one call from
/// <c>Program.cs</c> so an owner can see everything Ingestion wires without
/// reading the whole host.
/// </summary>
public static class IngestionModule
{
    public static IServiceCollection AddIngestion(this IServiceCollection services)
    {
        services.AddSingleton<IngestionStatusPublisher>();
        services.AddScoped<IngestionStore>();
        services.AddScoped<IngestionQueue>();
        services.AddScoped<IngestionSubmissionService>();

        // The ingestion-strategy registry (ADR-0004). Every strategy is registered as an
        // IIngestionStrategy; the registry keys them by their declared Document Type and
        // is the single authority both routing (the worker) and request validation
        // consult. A new Document Type is one more AddScoped line here — nothing else.
        // The prose strategies (transcript, note) are thin adapters over one shared
        // pipeline; they differ only in text source, chunk kind, and agent instructions.
        services.AddScoped<DocumentChunkCommitter>();
        services.AddScoped<PatientSummaryService>();
        services.AddScoped<ProseIngestionPipeline>();
        services.AddScoped<IIngestionStrategy, TranscriptIngestionStrategy>();
        services.AddScoped<IIngestionStrategy, DoctorNoteStrategy>();
        services.AddScoped<IIngestionStrategy, LabReportStrategy>();
        services.AddScoped<IIngestionStrategy, ImagingReportStrategy>();
        services.AddScoped<IngestionStrategyRegistry>();

        services.AddSingleton(Channel.CreateUnbounded<Guid>());
        services.AddHostedService<IngestionWorker>();
        services.AddHostedService<IngestionRecoverySweep>();

        return services;
    }
}
