using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Application.EventHandlers;

public sealed class ConsultationTranscriptReadyIntegrationEventHandler
    : IIntegrationEventHandler<ConsultationTranscriptReadyV1>
{
    public const string ConsumerName = "backend-clinical-knowledge";

    private readonly ITranscriptReadyPreparationStore _preparationStore;
    private readonly ILogger<ConsultationTranscriptReadyIntegrationEventHandler> _logger;

    public ConsultationTranscriptReadyIntegrationEventHandler(
        ITranscriptReadyPreparationStore preparationStore,
        ILogger<ConsultationTranscriptReadyIntegrationEventHandler> logger)
    {
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
            _logger.LogInformation(
                "Prepared session transcript request for consultation {ConsultationId}, transcript {TranscriptId}, revision {TranscriptRevision}.",
                envelope.Payload.ConsultationId,
                envelope.Payload.TranscriptId,
                envelope.Payload.TranscriptRevision);
            return;
        }

        _logger.LogInformation(
            "Skipped Transcript Ready event {EventId} for consultation {ConsultationId} with status {Status}.",
            envelope.EventId,
            envelope.Payload.ConsultationId,
            result.Status);
    }
}
