using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;
using MedicalAssistant.Transcription.Worker.Speech;
using MedicalAssistant.Transcription.Worker.Storage;
using MedicalAssistant.Transcription.Worker.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcription.Worker.Handlers;

public sealed class ConsultationAudioUploadedIntegrationEventHandler
    : IIntegrationEventHandler<ConsultationAudioUploadedV1>
{
    private readonly IConsultationAudioBlobRetriever _audioBlobRetriever;
    private readonly ISpeechTranscriptionService _speechTranscriptionService;
    private readonly ITranscriptionCompletionUnitOfWork _completionUnitOfWork;
    private readonly ITranscriptionInboxStore _inboxStore;
    private readonly RabbitMqTopologyOptions _topologyOptions;
    private readonly TranscriptionWorkerOptions _workerOptions;
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
        _inboxStore = inboxStore;
        _audioBlobRetriever = audioBlobRetriever;
        _speechTranscriptionService = speechTranscriptionService;
        _completionUnitOfWork = completionUnitOfWork;
        _topologyOptions = topologyOptions.Value;
        _workerOptions = workerOptions.Value;
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

        var consumerName = GetConsumerName();
        var claim = await _inboxStore.ClaimAsync(
            consumerName,
            envelope,
            $"{Environment.MachineName}:{Guid.NewGuid():N}",
            _workerOptions.ProcessingLeaseDuration,
            cancellationToken);

        if (claim.Status == TranscriptionInboxClaimStatus.DuplicateCompleted)
        {
            _logger.LogInformation(
                "Skipping duplicate completed transcription event {EventId} for consultation {ConsultationId}.",
                envelope.EventId,
                envelope.Payload.ConsultationId);
            return;
        }

        if (claim.Status == TranscriptionInboxClaimStatus.ActiveInProgress)
        {
            throw new TranscriptionInboxClaimException(
                "Transcription event is already being processed by another active worker lease.");
        }

        SpeechTranscriptionResult speechResult;
        try
        {
            var audio = await _audioBlobRetriever.OpenReadAsync(
                envelope.Payload.StorageObjectReference,
                cancellationToken);
            speechResult = await _speechTranscriptionService.TranscribeAsync(audio, cancellationToken);
        }
        catch (SpeechTranscriptionFailureException ex)
            when (ex.Category == SpeechTranscriptionFailureCategory.Permanent)
        {
            await _completionUnitOfWork.FailAsync(
                new TranscriptionFailureRequest(
                    consumerName,
                    envelope,
                    ex.Code,
                    "Permanent"),
                cancellationToken);
            return;
        }
        catch (BlobRetrievalFailureException ex)
            when (ex.Category == BlobRetrievalFailureCategory.PolicyViolation)
        {
            throw new NonRetryableIntegrationEventException(ex.Code);
        }

        await _completionUnitOfWork.CompleteAsync(
            new TranscriptionCompletionRequest(
                consumerName,
                envelope,
                speechResult.TranscriptText,
                ExternalJobId: null,
                speechResult.LanguageCode),
            cancellationToken);
    }

    private string GetConsumerName()
    {
        return string.IsNullOrWhiteSpace(_topologyOptions.SubscriberName)
            ? "transcription-worker"
            : _topologyOptions.SubscriberName;
    }
}
