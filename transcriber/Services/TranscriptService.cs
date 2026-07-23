using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.Transcriber.Models;
using MedicalAssistant.Transcriber.Persistence.Repositories.Interfaces;
using MedicalAssistant.Transcriber.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Transcriber.Services;

public sealed class TranscriptService : ITranscriptService
{
    private readonly ITranscriptRepository _transcriptRepository;
    private readonly IConsultationRepository _consultationRepository;
    private readonly ISpeechTranscriptionService _speechTranscription;
    private readonly ITranscriptReadyPublisher _transcriptReadyPublisher;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<TranscriptService> _logger;

    public TranscriptService(
        ITranscriptRepository transcriptRepository,
        IConsultationRepository consultationRepository,
        ISpeechTranscriptionService speechTranscription,
        ITranscriptReadyPublisher transcriptReadyPublisher,
        IAuditTrailService auditTrail,
        ILogger<TranscriptService> logger)
    {
        _transcriptRepository = transcriptRepository;
        _consultationRepository = consultationRepository;
        _speechTranscription = speechTranscription;
        _transcriptReadyPublisher = transcriptReadyPublisher;
        _auditTrail = auditTrail;
        _logger = logger;
    }

    public async Task CreateFromRetrievedFileAsync(
        ConsultationProcessingMessage message,
        RetrievedConsultationFile file,
        CancellationToken cancellationToken = default)
    {
        await _auditTrail.LogAsync(
            TranscriberAuditActions.TranscriptStarted,
            message,
            details: new
            {
                file.FileType,
                file.FileName,
                file.ByteLength,
                file.ContentType,
            },
            cancellationToken: cancellationToken);

        var consultation = await _consultationRepository.GetByIdAsync(
                message.ConsultationId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Consultation {message.ConsultationId} was not found when saving transcript.");

        var existing = await _transcriptRepository.GetByConsultationIdAsync(
            message.ConsultationId,
            cancellationToken);

        // Skip redelivered RabbitMQ messages when transcription already finished.
        if (IsAlreadyTranscribed(consultation, existing))
        {
            _logger.LogInformation(
                "Consultation {ConsultationId} already has a completed transcript; skipping duplicate processing.",
                message.ConsultationId);

            await _auditTrail.LogAsync(
                TranscriberAuditActions.DuplicateSkipped,
                message,
                details: new
                {
                    ConsultationStatus = consultation.Status.ToString(),
                    TranscriptId = existing?.Id,
                    TranscriptStatus = existing?.Status.ToString(),
                },
                cancellationToken: cancellationToken);

            // At-least-once: ensure consultation.transcript is enqueued even on redelivery.
            if (existing is not null)
                await PublishTranscriptReadyAsync(existing.Id, message, cancellationToken);

            return;
        }

        if (ShouldMarkTranscribing(consultation.Status))
        {
            var previousStatus = consultation.Status.ToString();
            consultation.MarkTranscribing();
            consultation.DateModified = DateTime.UtcNow;
            consultation.ModifiedBy = "transcriber-function";
            await _consultationRepository.UpdateAsync(consultation, cancellationToken);

            await _auditTrail.LogAsync(
                TranscriberAuditActions.ConsultationMarkedTranscribing,
                message,
                details: new
                {
                    PreviousStatus = previousStatus,
                    NewStatus = consultation.Status.ToString(),
                },
                cancellationToken: cancellationToken);
        }

        var transcriptText = await ResolveTranscriptTextAsync(message, file, cancellationToken);

        int transcriptId;
        var created = existing is null;
        if (existing is null)
        {
            var transcript = new Transcript
            {
                ConsultationId = message.ConsultationId,
                Status = TranscriptStatus.Pending,
                DateCreated = DateTime.UtcNow,
                CreatedBy = "transcriber-function",
            };
            transcript.MarkCompleted(transcriptText);
            await _transcriptRepository.AddAsync(transcript, cancellationToken);
            transcriptId = transcript.Id;
        }
        else
        {
            existing.MarkCompleted(transcriptText);
            existing.DateModified = DateTime.UtcNow;
            existing.ModifiedBy = "transcriber-function";
            await _transcriptRepository.UpdateAsync(existing, cancellationToken);
            transcriptId = existing.Id;
        }

        await _auditTrail.LogAsync(
            TranscriberAuditActions.TranscriptSaved,
            message,
            details: new
            {
                TranscriptId = transcriptId,
                Created = created,
                CharacterCount = transcriptText.Length,
                Preview = transcriptText.Length <= 200
                    ? transcriptText
                    : transcriptText[..200] + "…",
            },
            entityType: "Transcript",
            entityId: transcriptId.ToString(),
            cancellationToken: cancellationToken);

        await PublishTranscriptReadyAsync(transcriptId, message, cancellationToken);

        // Reload in case another worker updated status concurrently.
        consultation = await _consultationRepository.GetByIdAsync(
                message.ConsultationId,
                cancellationToken)
            ?? consultation;

        if (ShouldMarkTranscribed(consultation.Status))
        {
            var previousStatus = consultation.Status.ToString();
            consultation.MarkTranscribed();
            consultation.DateModified = DateTime.UtcNow;
            consultation.ModifiedBy = "transcriber-function";
            await _consultationRepository.UpdateAsync(consultation, cancellationToken);

            await _auditTrail.LogAsync(
                TranscriberAuditActions.ConsultationMarkedTranscribed,
                message,
                details: new
                {
                    PreviousStatus = previousStatus,
                    NewStatus = consultation.Status.ToString(),
                    TranscriptId = transcriptId,
                },
                cancellationToken: cancellationToken);
        }
    }

    private async Task PublishTranscriptReadyAsync(
        int transcriptId,
        ConsultationProcessingMessage message,
        CancellationToken cancellationToken)
    {
        await _transcriptReadyPublisher.PublishAsync(
            new TranscriptReadyMessage
            {
                EventType = TranscriptReadyEventTypes.Ready,
                TranscriptId = transcriptId,
                ConsultationId = message.ConsultationId,
                CorrelationId = message.CorrelationId,
                OccurredAtUtc = DateTime.UtcNow,
            },
            cancellationToken);

        await _auditTrail.LogAsync(
            TranscriberAuditActions.TranscriptReadyPublished,
            message,
            details: new
            {
                TranscriptId = transcriptId,
                Queue = "consultation.transcript",
                message.CorrelationId,
            },
            entityType: "Transcript",
            entityId: transcriptId.ToString(),
            cancellationToken: cancellationToken);
    }

    private async Task<string> ResolveTranscriptTextAsync(
        ConsultationProcessingMessage message,
        RetrievedConsultationFile file,
        CancellationToken cancellationToken)
    {
        if (string.Equals(file.FileType, ConsultationFileTypes.Document, StringComparison.OrdinalIgnoreCase))
        {
            var fileName = string.IsNullOrWhiteSpace(file.FileName) ? file.BlobName : file.FileName;
            _logger.LogInformation(
                "Skipping Azure Speech for document file {FileName}; storing placeholder transcript.",
                fileName);

            var placeholder =
                $"Document file '{fileName}' was received. Azure Speech transcription applies to audio only.";

            await _auditTrail.LogAsync(
                TranscriberAuditActions.DocumentPlaceholderUsed,
                message,
                details: new { fileName, file.FileType, CharacterCount = placeholder.Length },
                cancellationToken: cancellationToken);

            return placeholder;
        }

        await _auditTrail.LogAsync(
            TranscriberAuditActions.SpeechStarted,
            message,
            details: new
            {
                file.FileName,
                file.ContentType,
                file.ByteLength,
            },
            cancellationToken: cancellationToken);

        var transcriptText = await _speechTranscription.TranscribeAsync(file, cancellationToken);

        await _auditTrail.LogAsync(
            TranscriberAuditActions.SpeechCompleted,
            message,
            details: new
            {
                CharacterCount = transcriptText.Length,
                Preview = transcriptText.Length <= 200
                    ? transcriptText
                    : transcriptText[..200] + "…",
            },
            cancellationToken: cancellationToken);

        return transcriptText;
    }

    private static bool IsAlreadyTranscribed(Consultation consultation, Transcript? transcript) =>
        transcript is { Status: TranscriptStatus.Completed } &&
        !string.IsNullOrWhiteSpace(transcript.TranscriptText) &&
        consultation.Status is ConsultationStatus.Transcribed
            or ConsultationStatus.StructuredDataPending
            or ConsultationStatus.Completed;

    private static bool ShouldMarkTranscribing(ConsultationStatus status) =>
        status is ConsultationStatus.Draft
            or ConsultationStatus.AudioUploaded
            or ConsultationStatus.DocumentUploaded;

    private static bool ShouldMarkTranscribed(ConsultationStatus status) =>
        status is ConsultationStatus.Draft
            or ConsultationStatus.AudioUploaded
            or ConsultationStatus.DocumentUploaded
            or ConsultationStatus.Transcribing;
}
