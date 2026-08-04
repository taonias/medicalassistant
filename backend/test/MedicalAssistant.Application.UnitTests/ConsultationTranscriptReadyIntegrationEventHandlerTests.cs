using MedicalAssistant.Application.Contracts.ClinicalKnowledge;
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
        var client = new RecordingClinicalKnowledgeClient();
        var handler = new ConsultationTranscriptReadyIntegrationEventHandler(
            client,
            store,
            NullLogger<ConsultationTranscriptReadyIntegrationEventHandler>.Instance);
        var envelope = CreateEnvelope();

        await handler.HandleAsync(envelope, CancellationToken.None);

        Assert.Equal(ConsultationTranscriptReadyIntegrationEventHandler.ConsumerName, store.ConsumerName);
        Assert.Same(envelope, store.Envelope);
        Assert.NotNull(client.Request);
        Assert.Equal("SessionTranscript", client.Request.DocumentType);
        Assert.Equal("doctor-1", client.Request.DoctorId);
        Assert.Equal("patient-ext-5", client.Request.PatientId);
        Assert.Equal("10", client.Request.SessionId);
        Assert.Equal(2, client.Request.SequenceNumber);
        Assert.Equal("el-GR", client.Request.Language);
        Assert.Equal("clinical transcript text", client.Request.Transcript);
        Assert.DoesNotContain("clinical transcript text", IntegrationEventSerializer.Serialize(envelope));
    }

    [Fact]
    public async Task HandleAsync_does_not_call_clinical_knowledge_when_preparation_skips_event()
    {
        var store = new RecordingTranscriptReadyPreparationStore(TranscriptReadyPreparationStatus.IgnoredDeleted);
        var client = new RecordingClinicalKnowledgeClient();
        var handler = new ConsultationTranscriptReadyIntegrationEventHandler(
            client,
            store,
            NullLogger<ConsultationTranscriptReadyIntegrationEventHandler>.Instance);

        await handler.HandleAsync(CreateEnvelope(), CancellationToken.None);

        Assert.Null(client.Request);
    }

    private sealed class RecordingTranscriptReadyPreparationStore : ITranscriptReadyPreparationStore
    {
        private readonly TranscriptReadyPreparationStatus _status;

        public RecordingTranscriptReadyPreparationStore(
            TranscriptReadyPreparationStatus status = TranscriptReadyPreparationStatus.Prepared)
        {
            _status = status;
        }

        public string? ConsumerName { get; private set; }
        public IntegrationEventEnvelope<ConsultationTranscriptReadyV1>? Envelope { get; private set; }

        public Task<TranscriptReadyPreparationResult> PrepareAsync(
            string consumerName,
            IntegrationEventEnvelope<ConsultationTranscriptReadyV1> envelope,
            CancellationToken cancellationToken = default)
        {
            ConsumerName = consumerName;
            Envelope = envelope;
            if (_status != TranscriptReadyPreparationStatus.Prepared)
            {
                return Task.FromResult(new TranscriptReadyPreparationResult(_status, Request: null));
            }

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

    private sealed class RecordingClinicalKnowledgeClient : IClinicalKnowledgeClient
    {
        public ClinicalKnowledgeSessionTranscriptRequest? Request { get; private set; }

        public Task<ClinicalKnowledgeIngestionAccepted> SubmitSessionTranscriptAsync(
            ClinicalKnowledgeSessionTranscriptRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new ClinicalKnowledgeIngestionAccepted(
                Guid.Parse("aaaaaaaa-1111-1111-1111-aaaaaaaaaaaa"),
                Duplicate: false));
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
