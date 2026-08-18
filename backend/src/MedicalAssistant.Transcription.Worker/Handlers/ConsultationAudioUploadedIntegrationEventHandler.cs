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

        if (claim.Status is TranscriptionInboxClaimStatus.SkippedDeleted or TranscriptionInboxClaimStatus.SkippedSuperseded)
        {
            _logger.LogInformation(
                "Skipping transcription event {EventId} for consultation {ConsultationId} because state gate returned {ClaimStatus}.",
                envelope.EventId,
                envelope.Payload.ConsultationId,
                claim.Status);
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
        // Automatic retries are disabled. Every transcription failure - whether classified
        // Transient or Permanent - is recorded to the database as Failed with its code, so the
        // doctor sees it and can trigger a manual retry from the UI. Nothing is re-queued.
        catch (SpeechTranscriptionFailureException ex)
        {
            await _completionUnitOfWork.FailAsync(
                new TranscriptionFailureRequest(
                    consumerName,
                    envelope,
                    ex.Code,
                    ex.Category.ToString()),
                cancellationToken);
            return;
        }
        // A blob policy violation is genuine poison (e.g. a reference outside the private
        // container); dead-letter it rather than record it as a retryable transcription failure.
        catch (BlobRetrievalFailureException ex)
            when (ex.Category == BlobRetrievalFailureCategory.PolicyViolation)
        {
            throw new NonRetryableIntegrationEventException(ex.Code);
        }
        // Missing or transiently-unavailable audio is recorded as a failure for manual retry.
        catch (BlobRetrievalFailureException ex)
        {
            await _completionUnitOfWork.FailAsync(
                new TranscriptionFailureRequest(
                    consumerName,
                    envelope,
                    ex.Code,
                    ex.Category.ToString()),
                cancellationToken);
            return;
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
