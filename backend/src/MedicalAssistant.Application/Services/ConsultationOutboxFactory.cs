using MedicalAssistant.Domain;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using System.Text.Json;

namespace MedicalAssistant.Application.Services;

public static class ConsultationOutboxFactory
{
    public static ConsultationOutboxMessage AudioUploaded(
        Consultation consultation,
        string fileId,
        string correlationId)
    {
        var payload = new ConsultationAudioUploadedV1(
            consultation.Id,
            fileId,
            consultation.AudioContentType ?? "application/octet-stream",
            consultation.SourceObjectReference ?? consultation.AudioBlobUri ?? string.Empty,
            consultation.DurationSeconds);

        return FromEnvelope(
            IntegrationEventEnvelope.Create(
                payload,
                ConsultationIntegrationEvents.Registry.Resolve<ConsultationAudioUploadedV1>(),
                "medicalassistant.backend",
                correlationId,
                null),
            consultation.Id);
    }

    public static ConsultationOutboxMessage DocumentUploaded(
        Consultation consultation,
        string fileId,
        string correlationId)
    {
        var payload = new ConsultationDocumentUploadedV1(
            consultation.Id,
            fileId,
            null,
            consultation.DocumentContentType ?? "application/octet-stream",
            consultation.SourceObjectReference ?? consultation.DocumentBlobUri ?? string.Empty);

        return FromEnvelope(
            IntegrationEventEnvelope.Create(
                payload,
                ConsultationIntegrationEvents.Registry.Resolve<ConsultationDocumentUploadedV1>(),
                "medicalassistant.backend",
                correlationId,
                null),
            consultation.Id);
    }

    private static ConsultationOutboxMessage FromEnvelope<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        int consultationId)
    {
        return new ConsultationOutboxMessage
        {
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            EventVersion = envelope.EventVersion,
            OccurredAtUtc = envelope.OccurredAtUtc,
            Producer = envelope.Producer,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            AggregateType = nameof(Consultation),
            AggregateId = consultationId.ToString(),
            Payload = JsonSerializer.Serialize(envelope.Payload, IntegrationEventSerializer.Options),
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
