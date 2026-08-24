using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Services;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Repositories;

public sealed class ConsultationRetryStore : IConsultationRetryStore
{
    private readonly MedicalAssistantDatabaseContext _context;

    public ConsultationRetryStore(MedicalAssistantDatabaseContext context)
    {
        _context = context;
    }

    public async Task<ConsultationRetryOutcome> RequeueAsync(
        int consultationId,
        string doctorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(doctorId);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var consultation = await _context.Consultations
            .SingleOrDefaultAsync(c => c.Id == consultationId && c.DoctorId == doctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Consultation), consultationId);

        var correlationId = Guid.NewGuid().ToString("N");
        ConsultationOutboxMessage outboxMessage;
        ConsultationRetryKind kind;

        if (consultation.Status == ConsultationStatus.Failed)
        {
            // Transcription failed - return to AudioUploaded and re-run the worker from the
            // stored audio via a fresh audio-uploaded event.
            var storageReference = consultation.SourceObjectReference ?? consultation.AudioBlobUri;
            if (string.IsNullOrWhiteSpace(storageReference))
            {
                throw new BadRequestException(
                    "No audio is available to retry transcription for this consultation.");
            }

            consultation.MarkForTranscriptionRetry();
            outboxMessage = ConsultationOutboxFactory.AudioUploaded(
                consultation,
                storageReference,
                correlationId);
            kind = ConsultationRetryKind.Transcription;
        }
        else if (!string.IsNullOrWhiteSpace(consultation.FailureReason))
        {
            // Transcript is valid but downstream clinical-knowledge indexing failed - clear the
            // failure and re-run ingestion via a fresh transcript-ready event.
            var transcript = await _context.Transcripts
                .SingleOrDefaultAsync(t => t.ConsultationId == consultationId, cancellationToken)
                ?? throw new BadRequestException(
                    "No transcript is available to retry indexing for this consultation.");

            if (transcript.Status != TranscriptStatus.Completed ||
                string.IsNullOrWhiteSpace(transcript.TranscriptText))
            {
                throw new BadRequestException("The transcript is not complete; cannot retry indexing.");
            }

            consultation.ClearFailureReason();
            outboxMessage = ConsultationOutboxFactory.TranscriptReady(
                consultation,
                transcript,
                correlationId);
            kind = ConsultationRetryKind.Indexing;
        }
        else
        {
            throw new BadRequestException("This consultation has no failure to retry.");
        }

        await _context.ConsultationOutboxMessages.AddAsync(outboxMessage, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ConsultationRetryOutcome(kind, consultationId);
    }
}
