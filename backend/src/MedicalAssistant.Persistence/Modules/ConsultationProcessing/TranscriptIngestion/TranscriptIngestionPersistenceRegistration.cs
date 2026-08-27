using MedicalAssistant.Application.Contracts.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Persistence.Modules.ConsultationProcessing.TranscriptIngestion;

/// <summary>
/// Composition root for Consultation Processing's transcript-ingestion
/// persistence (R38). One call from <see cref="PersistenceServiceRegistration"/>
/// so an owner can see everything Transcript Ingestion wires here without
/// reading the whole registration file.
/// </summary>
public static class TranscriptIngestionPersistenceRegistration
{
    public static IServiceCollection AddTranscriptIngestionPersistence(this IServiceCollection services)
    {
        services.AddScoped<ITranscriptReadyPreparationStore, TranscriptReadyPreparationStore>();

        return services;
    }
}
