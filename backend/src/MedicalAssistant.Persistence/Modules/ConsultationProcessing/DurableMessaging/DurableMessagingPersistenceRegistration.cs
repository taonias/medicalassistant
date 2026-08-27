using MedicalAssistant.Application.Contracts.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Persistence.Modules.ConsultationProcessing.DurableMessaging;

/// <summary>
/// Composition root for Consultation Processing's durable-messaging persistence
/// (R38): the transcription inbox, the transcription-completion unit of work,
/// and the consultation outbox store. One call from
/// <see cref="PersistenceServiceRegistration"/> so an owner can see everything
/// Durable Messaging wires here without reading the whole registration file.
/// </summary>
public static class DurableMessagingPersistenceRegistration
{
    public static IServiceCollection AddDurableMessagingPersistence(this IServiceCollection services)
    {
        services.AddScoped<ITranscriptionInboxStore, TranscriptionInboxStore>();
        services.AddScoped<ITranscriptionCompletionUnitOfWork, TranscriptionCompletionUnitOfWork>();
        services.AddScoped<IConsultationOutboxStore, ConsultationOutboxStore>();

        return services;
    }
}
