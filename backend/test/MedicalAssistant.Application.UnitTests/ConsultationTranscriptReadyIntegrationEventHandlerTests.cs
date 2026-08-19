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
        Assert.Equal("doctor-1#patient-ext-5#10#2", client.Request.DocumentId);
        Assert.Equal("el-GR", client.Request.Language);
        Assert.Equal("clinical transcript text", client.Request.Transcript);
        Assert.NotNull(store.Accepted);
        Assert.Equal("doctor-1#patient-ext-5#10#2", store.Accepted.DocumentId);
        Assert.Equal(Guid.Parse("aaaaaaaa-1111-1111-1111-aaaaaaaaaaaa"), store.Accepted.IngestionId);
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
        Assert.Null(store.Accepted);
    }

    [Fact]
    public async Task HandleAsync_records_failure_and_does_not_rethrow_when_ingestion_fails()
    {
        // Automatic retries are disabled: an ingestion failure must be recorded (for the doctor
        // to retry manually) and the message acknowledged, so the handler must not rethrow.
        var store = new RecordingTranscriptReadyPreparationStore();
        var client = new RecordingClinicalKnowledgeClient(throwOnSubmit: true);
        var handler = new ConsultationTranscriptReadyIntegrationEventHandler(
            client,
            store,
            NullLogger<ConsultationTranscriptReadyIntegrationEventHandler>.Instance);

        await handler.HandleAsync(CreateEnvelope(), CancellationToken.None);

        Assert.True(store.RecordFailedCalled);
        Assert.Equal("clinical-knowledge-ingestion-failed", store.FailedCode);
        Assert.Null(store.Accepted);
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
        public TranscriptReadyAcceptedResult? Accepted { get; private set; }
        public bool RecordFailedCalled { get; private set; }
        public string? FailedCode { get; private set; }

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

        public Task CompleteAcceptedAsync(
            string consumerName,
            IntegrationEventEnvelope<ConsultationTranscriptReadyV1> envelope,
            TranscriptReadyAcceptedResult accepted,
            CancellationToken cancellationToken = default)
        {
            ConsumerName = consumerName;
            Envelope = envelope;
            Accepted = accepted;
            return Task.CompletedTask;
        }

        public Task RecordFailedAsync(
            string consumerName,
            IntegrationEventEnvelope<ConsultationTranscriptReadyV1> envelope,
            string failureCode,
            CancellationToken cancellationToken = default)
        {
            ConsumerName = consumerName;
            Envelope = envelope;
            RecordFailedCalled = true;
            FailedCode = failureCode;
            return Task.CompletedTask;
        }

        public Task RecordIngestionOutcomeAsync(
            int consultationId,
            bool succeeded,
            string? failureReason,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingClinicalKnowledgeClient : IClinicalKnowledgeClient
    {
        private readonly bool _throwOnSubmit;

        public RecordingClinicalKnowledgeClient(bool throwOnSubmit = false)
        {
            _throwOnSubmit = throwOnSubmit;
        }

        public ClinicalKnowledgeSessionTranscriptRequest? Request { get; private set; }

        public Task<ClinicalKnowledgeIngestionAccepted> SubmitSessionTranscriptAsync(
            ClinicalKnowledgeSessionTranscriptRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            if (_throwOnSubmit)
            {
                throw new InvalidOperationException("clinical knowledge unavailable");
            }

            return Task.FromResult(new ClinicalKnowledgeIngestionAccepted(
                Guid.Parse("aaaaaaaa-1111-1111-1111-aaaaaaaaaaaa"),
                Duplicate: false));
        }

        public Task<ClinicalKnowledgeUnIngestResult> UnIngestDocumentAsync(
            string documentId,
            string removedBy,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ClinicalKnowledgeUnIngestResult(
                documentId,
                ClinicalKnowledgeUnIngestStatus.Removed));

        public Task<ClinicalKnowledgeAnswer> GetGroundedAnswerAsync(
            ClinicalKnowledgeChatRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
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
