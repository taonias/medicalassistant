using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;
using MedicalAssistant.Transcription.Worker.Speech;
using MedicalAssistant.Transcription.Worker.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcription.Worker.Handlers;

public sealed class ConsultationAudioUploadedIntegrationEventHandler
    : IIntegrationEventHandler<ConsultationAudioUploadedV1>
{
    private readonly IConsultationAudioBlobRetriever _audioBlobRetriever;
    private readonly ISpeechTranscriptionService _speechTranscriptionService;
    private readonly ITranscriptionCompletionUnitOfWork _completionUnitOfWork;
    private readonly RabbitMqTopologyOptions _topologyOptions;
    private readonly ILogger<ConsultationAudioUploadedIntegrationEventHandler> _logger;

    public ConsultationAudioUploadedIntegrationEventHandler(
        IConsultationAudioBlobRetriever audioBlobRetriever,
        ISpeechTranscriptionService speechTranscriptionService,
        ITranscriptionCompletionUnitOfWork completionUnitOfWork,
        IOptions<RabbitMqTopologyOptions> topologyOptions,
        ILogger<ConsultationAudioUploadedIntegrationEventHandler> logger)
    {
        _audioBlobRetriever = audioBlobRetriever;
        _speechTranscriptionService = speechTranscriptionService;
        _completionUnitOfWork = completionUnitOfWork;
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

        var audio = await _audioBlobRetriever.OpenReadAsync(
            envelope.Payload.StorageObjectReference,
            cancellationToken);
        var speechResult = await _speechTranscriptionService.TranscribeAsync(audio, cancellationToken);

        var consumerName = string.IsNullOrWhiteSpace(_topologyOptions.SubscriberName)
            ? "transcription-worker"
            : _topologyOptions.SubscriberName;
        await _completionUnitOfWork.CompleteAsync(
            new TranscriptionCompletionRequest(
                consumerName,
                envelope,
                speechResult.TranscriptText,
                ExternalJobId: null,
                speechResult.LanguageCode),
            cancellationToken);
    }
}
