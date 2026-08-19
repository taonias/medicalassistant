using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Application.EventHandlers;

/// <summary>
/// Reconciles a clinical-knowledge ingestion that failed out of band, reported by the AI
/// service over the event bus. Sets a doctor-visible, retryable failure reason on the
/// consultation. Idempotent (re-setting the same reason is a no-op), so at-least-once
/// redelivery is safe and no inbox is needed.
/// </summary>
public sealed class ConsultationIngestionFailedIntegrationEventHandler
    : IIntegrationEventHandler<ConsultationIngestionFailedV1>
{
    private readonly ITranscriptReadyPreparationStore _store;
    private readonly ILogger<ConsultationIngestionFailedIntegrationEventHandler> _logger;

    public ConsultationIngestionFailedIntegrationEventHandler(
        ITranscriptReadyPreparationStore store,
        ILogger<ConsultationIngestionFailedIntegrationEventHandler> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task HandleAsync(
        IntegrationEventEnvelope<ConsultationIngestionFailedV1> envelope,
        CancellationToken cancellationToken)
    {
        var payload = envelope.Payload;

        // The AI service's session id is the backend consultation id; anything else is not a
        // consultation-linked ingestion and is ignored.
        if (!int.TryParse(payload.SessionId, out var consultationId))
        {
            _logger.LogWarning(
                "Ignoring clinical-knowledge ingestion-failed event {EventId} with non-numeric session id.",
                envelope.EventId);
            return;
        }

        await _store.RecordIngestionOutcomeAsync(
            consultationId, succeeded: false, payload.Reason, cancellationToken);

        _logger.LogInformation(
            "Recorded clinical-knowledge ingestion failure for consultation {ConsultationId}, ingestion {IngestionId}.",
            consultationId, payload.IngestionId);
    }
}
