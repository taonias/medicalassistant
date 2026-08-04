using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Transcription.Worker.Handlers;

public sealed class ConsultationAudioUploadedIntegrationEventHandler
    : IIntegrationEventHandler<ConsultationAudioUploadedV1>
{
    private readonly ILogger<ConsultationAudioUploadedIntegrationEventHandler> _logger;

    public ConsultationAudioUploadedIntegrationEventHandler(
        ILogger<ConsultationAudioUploadedIntegrationEventHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        IntegrationEventEnvelope<ConsultationAudioUploadedV1> envelope,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received transcription work item {EventId} for consultation {ConsultationId}.",
            envelope.EventId,
            envelope.Payload.ConsultationId);

        throw new NotImplementedException(
            "Audio transcription processing is implemented by the T18 worker handler task.");
    }
}
