using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;
using MedicalAssistant.Transcription.Worker.Application.TranscribeAudio;
using MedicalAssistant.Transcription.Worker.Speech;
using MedicalAssistant.Transcription.Worker.Storage;
using MedicalAssistant.Transcription.Worker.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcription.Worker.Handlers;

/// <summary>
/// The RabbitMQ/EventBus edge (R24): the only piece of this worker that knows
/// about integration-event dispatch and <see cref="RabbitMqTopologyOptions"/>.
/// Maps a delivered envelope onto a <see cref="TranscribeAudioWorkItem"/> and
/// hands it to <see cref="TranscribeAudioWorkflow"/>, which owns the actual
/// transcription policy and knows nothing about the broker.
/// </summary>
public sealed class ConsultationAudioUploadedIntegrationEventHandler
    : IIntegrationEventHandler<ConsultationAudioUploadedV1>
{
    private readonly TranscribeAudioWorkflow _workflow;
    private readonly RabbitMqTopologyOptions _topologyOptions;
    private readonly ILogger<ConsultationAudioUploadedIntegrationEventHandler> _logger;

    public ConsultationAudioUploadedIntegrationEventHandler(
        ITranscriptionInboxStore inboxStore,
        IConsultationAudioBlobRetriever audioBlobRetriever,
        ISpeechTranscriptionService speechTranscriptionService,
        ITranscriptionCompletionUnitOfWork completionUnitOfWork,
        IOptions<RabbitMqTopologyOptions> topologyOptions,
        IOptions<TranscriptionWorkerOptions> workerOptions,
        ILogger<ConsultationAudioUploadedIntegrationEventHandler> logger)
    {
        _workflow = new TranscribeAudioWorkflow(
            inboxStore,
            audioBlobRetriever,
            speechTranscriptionService,
            completionUnitOfWork,
            workerOptions,
            logger);
        _topologyOptions = topologyOptions.Value;
        _logger = logger;
    }

    public async Task HandleAsync(
        IntegrationEventEnvelope<ConsultationAudioUploadedV1> envelope,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received transcription work item {EventId} for consultation {ConsultationId}.",
            envelope.EventId,
            envelope.Payload.ConsultationId);

        var workItem = new TranscribeAudioWorkItem(
            envelope,
            GetConsumerName(),
            $"{Environment.MachineName}:{Guid.NewGuid():N}");

        await _workflow.RunAsync(workItem, cancellationToken);
    }

    private string GetConsumerName()
    {
        return string.IsNullOrWhiteSpace(_topologyOptions.SubscriberName)
            ? "transcription-worker"
            : _topologyOptions.SubscriberName;
    }
}
