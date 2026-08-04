using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.EventHandlers;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace MedicalAssistant.Application.UnitTests;

public class ConsultationTranscriptReadyIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_prepares_backend_session_transcript_request_without_message_text_payload()
    {
        var store = new RecordingTranscriptReadyPreparationStore();
        var handler = new ConsultationTranscriptReadyIntegrationEventHandler(
            store,
            NullLogger<ConsultationTranscriptReadyIntegrationEventHandler>.Instance);
        var envelope = CreateEnvelope();

        await handler.HandleAsync(envelope, CancellationToken.None);

        Assert.Equal(ConsultationTranscriptReadyIntegrationEventHandler.ConsumerName, store.ConsumerName);
        Assert.Same(envelope, store.Envelope);
        Assert.DoesNotContain("clinical transcript text", IntegrationEventSerializer.Serialize(envelope));
    }

    private sealed class RecordingTranscriptReadyPreparationStore : ITranscriptReadyPreparationStore
    {
        public string? ConsumerName { get; private set; }
        public IntegrationEventEnvelope<ConsultationTranscriptReadyV1>? Envelope { get; private set; }

        public Task<TranscriptReadyPreparationResult> PrepareAsync(
            string consumerName,
            IntegrationEventEnvelope<ConsultationTranscriptReadyV1> envelope,
            CancellationToken cancellationToken = default)
        {
            ConsumerName = consumerName;
            Envelope = envelope;
            return Task.FromResult(new TranscriptReadyPreparationResult(
                TranscriptReadyPreparationStatus.Prepared,
                new PreparedSessionTranscriptRequest(
                    envelope.Payload.ConsultationId,
                    envelope.Payload.TranscriptId,
                    envelope.Payload.TranscriptRevision,
                    PatientId: 5,
                    PatientExternalId: "patient-ext-5",
                    PatientDisplayName: "Ada Lovelace",
                    DoctorId: "doctor-1",
                    ConsultationDate: DateTime.UtcNow,
                    envelope.Payload.LanguageCode,
                    "clinical transcript text",
                    envelope.CorrelationId ?? "correlation-1")));
        }
    }

    private static IntegrationEventEnvelope<ConsultationTranscriptReadyV1> CreateEnvelope()
    {
        return new IntegrationEventEnvelope<ConsultationTranscriptReadyV1>(
            Guid.Parse("55555555-aaaa-aaaa-aaaa-555555555555"),
            ConsultationIntegrationEvents.TranscriptReadyV1,
            1,
            DateTime.UtcNow,
            "medicalassistant.backend",
            "correlation-1",
            null,
            new ConsultationTranscriptReadyV1(
                10,
                "file-10",
                100,
                2,
                "el-GR"));
    }
}
