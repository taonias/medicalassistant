using MedicalAssistant.Application.Contracts.ClinicalKnowledge;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Application.EventHandlers;

public sealed class ConsultationTranscriptReadyIntegrationEventHandler
    : IIntegrationEventHandler<ConsultationTranscriptReadyV1>
{
    public const string ConsumerName = "backend-clinical-knowledge";

    private readonly ITranscriptIngestionGateway _clinicalKnowledgeClient;
    private readonly ITranscriptReadyPreparationStore _preparationStore;
    private readonly ILogger<ConsultationTranscriptReadyIntegrationEventHandler> _logger;

    public ConsultationTranscriptReadyIntegrationEventHandler(
        ITranscriptIngestionGateway clinicalKnowledgeClient,
        ITranscriptReadyPreparationStore preparationStore,
        ILogger<ConsultationTranscriptReadyIntegrationEventHandler> logger)
    {
        _clinicalKnowledgeClient = clinicalKnowledgeClient;
        _preparationStore = preparationStore;
        _logger = logger;
    }

    public async Task HandleAsync(
        IntegrationEventEnvelope<ConsultationTranscriptReadyV1> envelope,
        CancellationToken cancellationToken)
    {
        var result = await _preparationStore.PrepareAsync(
            ConsumerName,
            envelope,
            cancellationToken);

        if (result.Status == TranscriptReadyPreparationStatus.Prepared)
        {
            var prepared = result.Request
                ?? throw new InvalidOperationException("Prepared transcript result did not include a request.");
            var request = MapToClinicalKnowledgeRequest(prepared);

            try
            {
                var accepted = await _clinicalKnowledgeClient.SubmitSessionTranscriptAsync(
                    request,
                    cancellationToken);
                await _preparationStore.CompleteAcceptedAsync(
                    ConsumerName,
                    envelope,
                    new TranscriptReadyAcceptedResult(
                        accepted.IngestionId,
                        request.DocumentId,
                        accepted.Duplicate),
                    cancellationToken);

                _logger.LogInformation(
                    "Submitted session transcript request for consultation {ConsultationId}, transcript {TranscriptId}, revision {TranscriptRevision}; ingestion {IngestionId}, duplicate {Duplicate}.",
                    envelope.Payload.ConsultationId,
                    envelope.Payload.TranscriptId,
                    envelope.Payload.TranscriptRevision,
                    accepted.IngestionId,
                    accepted.Duplicate);
            }
            // Automatic retries are disabled. Record the indexing failure so it is durable and
            // visible to the doctor, who retries manually; the message is then acknowledged
            // (not re-queued). Unexpected structural problems raised earlier by PrepareAsync are
            // left to bubble to the dead-letter queue.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await _preparationStore.RecordFailedAsync(
                    ConsumerName,
                    envelope,
                    "clinical-knowledge-ingestion-failed",
                    cancellationToken);

                _logger.LogError(
                    ex,
                    "Clinical Knowledge ingestion failed for consultation {ConsultationId}, transcript {TranscriptId}, revision {TranscriptRevision}; recorded as failed for manual retry.",
                    envelope.Payload.ConsultationId,
                    envelope.Payload.TranscriptId,
                    envelope.Payload.TranscriptRevision);
            }

            return;
        }

        _logger.LogInformation(
            "Skipped Transcript Ready event {EventId} for consultation {ConsultationId} with status {Status}.",
            envelope.EventId,
            envelope.Payload.ConsultationId,
            result.Status);
    }

    private static ClinicalKnowledgeSessionTranscriptRequest MapToClinicalKnowledgeRequest(
        PreparedSessionTranscriptRequest prepared)
    {
        var patientId = string.IsNullOrWhiteSpace(prepared.PatientExternalId)
            ? prepared.PatientId.ToString()
            : prepared.PatientExternalId;

        return new ClinicalKnowledgeSessionTranscriptRequest(
            prepared.DoctorId,
            patientId,
            SessionId: prepared.ConsultationId.ToString(),
            SequenceNumber: prepared.TranscriptRevision,
            SessionDate: new DateTimeOffset(prepared.ConsultationDate, TimeSpan.Zero),
            prepared.LanguageCode,
            prepared.TranscriptText);
    }
}
