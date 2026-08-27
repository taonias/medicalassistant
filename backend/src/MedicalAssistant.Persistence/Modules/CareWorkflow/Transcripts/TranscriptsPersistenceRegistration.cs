using MedicalAssistant.Application.Contracts.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Persistence.Modules.CareWorkflow.Transcripts;

/// <summary>
/// Composition root for the Transcripts module's persistence (R38). One call
/// from <see cref="PersistenceServiceRegistration"/> so an owner can see
/// everything Transcripts wires here without reading the whole registration file.
/// </summary>
public static class TranscriptsPersistenceRegistration
{
    public static IServiceCollection AddTranscriptsPersistence(this IServiceCollection services)
    {
        services.AddScoped<ITranscriptRepository, TranscriptRepository>();

        return services;
    }
}
