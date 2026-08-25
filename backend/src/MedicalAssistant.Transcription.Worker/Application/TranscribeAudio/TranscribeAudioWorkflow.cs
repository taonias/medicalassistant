using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.EventBus;
using MedicalAssistant.Transcription.Worker.Handlers;
using MedicalAssistant.Transcription.Worker.Options;
using MedicalAssistant.Transcription.Worker.Speech;
using MedicalAssistant.Transcription.Worker.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcription.Worker.Application.TranscribeAudio;

/// <summary>
/// The complete Transcribe Audio policy (R24): dedup/lease claim, audio
/// retrieval, transcription, and the completion or failure commit. Knows
/// nothing about RabbitMQ or how its work item was delivered — the Host edge
/// (<see cref="Transcription.Worker.Handlers.ConsultationAudioUploadedIntegrationEventHandler"/>)
/// maps a broker delivery onto a <see cref="TranscribeAudioWorkItem"/> and
/// hands it here.
/// </summary>
internal sealed class TranscribeAudioWorkflow(
    ITranscriptionInboxStore inboxStore,
    IConsultationAudioBlobRetriever audioBlobRetriever,
    ISpeechTranscriptionService speechTranscriptionService,
    ITranscriptionCompletionUnitOfWork completionUnitOfWork,
    IOptions<TranscriptionWorkerOptions> workerOptions,
    ILogger logger)
{
    public async Task RunAsync(TranscribeAudioWorkItem workItem, CancellationToken cancellationToken)
    {
        var envelope = workItem.Envelope;
        var claim = await inboxStore.ClaimAsync(
            workItem.ConsumerName,
            envelope,
            workItem.LeaseOwnerToken,
            workerOptions.Value.ProcessingLeaseDuration,
            cancellationToken);

        if (claim.Status == TranscriptionInboxClaimStatus.DuplicateCompleted)
        {
            logger.LogInformation(
                "Skipping duplicate completed transcription event {EventId} for consultation {ConsultationId}.",
                envelope.EventId,
                envelope.Payload.ConsultationId);
            return;
        }

        if (claim.Status is TranscriptionInboxClaimStatus.SkippedDeleted or TranscriptionInboxClaimStatus.SkippedSuperseded)
        {
            logger.LogInformation(
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
            var audio = await audioBlobRetriever.OpenReadAsync(
                envelope.Payload.StorageObjectReference,
                cancellationToken);
            speechResult = await speechTranscriptionService.TranscribeAsync(audio, cancellationToken);
        }
        // Automatic retries are disabled. Every transcription failure - whether classified
        // Transient or Permanent - is recorded to the database as Failed with its code, so the
        // doctor sees it and can trigger a manual retry from the UI. Nothing is re-queued.
        catch (SpeechTranscriptionFailureException ex)
        {
            await completionUnitOfWork.FailAsync(
                new TranscriptionFailureRequest(
                    workItem.ConsumerName,
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
            await completionUnitOfWork.FailAsync(
                new TranscriptionFailureRequest(
                    workItem.ConsumerName,
                    envelope,
                    ex.Code,
                    ex.Category.ToString()),
                cancellationToken);
            return;
        }

        await completionUnitOfWork.CompleteAsync(
            new TranscriptionCompletionRequest(
                workItem.ConsumerName,
                envelope,
                speechResult.TranscriptText,
                ExternalJobId: null,
                speechResult.LanguageCode),
            cancellationToken);
    }
}
