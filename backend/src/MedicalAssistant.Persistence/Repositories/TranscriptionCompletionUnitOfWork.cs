using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Services;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Repositories;

public sealed class TranscriptionCompletionUnitOfWork : ITranscriptionCompletionUnitOfWork
{
    private readonly MedicalAssistantDatabaseContext _context;

    public TranscriptionCompletionUnitOfWork(MedicalAssistantDatabaseContext context)
    {
        _context = context;
    }

    public async Task<TranscriptionCompletionResult> CompleteAsync(
        TranscriptionCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ConsumerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TranscriptText);

        var payload = request.Envelope.Payload;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var inbox = await _context.ConsultationInboxMessages
            .SingleOrDefaultAsync(message =>
                    message.ConsumerName == request.ConsumerName &&
                    message.EventId == request.Envelope.EventId,
                cancellationToken);

        if (inbox?.Status == ConsultationEventMessageStatus.Completed)
        {
            await transaction.CommitAsync(cancellationToken);
            var existingTranscriptId = await _context.Transcripts
                .Where(transcript => transcript.ConsultationId == payload.ConsultationId)
                .Select(transcript => (int?)transcript.Id)
                .SingleOrDefaultAsync(cancellationToken);

            return new TranscriptionCompletionResult(
                TranscriptionCompletionStatus.DuplicateCompleted,
                payload.ConsultationId,
                existingTranscriptId);
        }

        var now = DateTime.UtcNow;
        if (inbox is null)
        {
            inbox = new ConsultationInboxMessage
            {
                ConsumerName = request.ConsumerName,
                EventId = request.Envelope.EventId,
                EventType = request.Envelope.EventType,
                EventVersion = request.Envelope.EventVersion,
                ReceivedAtUtc = now
            };
            await _context.ConsultationInboxMessages.AddAsync(inbox, cancellationToken);
        }

        inbox.Status = ConsultationEventMessageStatus.Completed;
        inbox.AttemptCount++;
        inbox.LastAttemptAtUtc = now;
        inbox.CompletedAtUtc = now;
        inbox.LeaseOwner = null;
        inbox.LeaseExpiresAtUtc = null;
        inbox.LastFailureCategory = null;
        inbox.LastFailureCode = null;

        var consultation = await _context.Consultations
            .SingleOrDefaultAsync(c => c.Id == payload.ConsultationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Consultation), payload.ConsultationId);

        var transcript = await _context.Transcripts
            .SingleOrDefaultAsync(t => t.ConsultationId == payload.ConsultationId, cancellationToken);

        if (transcript is null)
        {
            transcript = new Transcript
            {
                ConsultationId = payload.ConsultationId
            };
            await _context.Transcripts.AddAsync(transcript, cancellationToken);
        }

        transcript.ExternalJobId = request.ExternalJobId;
        transcript.MarkCompleted(request.TranscriptText);
        consultation.MarkTranscribed();

        await _context.SaveChangesAsync(cancellationToken);

        var correlationId = string.IsNullOrWhiteSpace(request.Envelope.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : request.Envelope.CorrelationId;
        var outboxMessage = ConsultationOutboxFactory.TranscriptReady(
            consultation,
            transcript,
            correlationId);
        await _context.ConsultationOutboxMessages.AddAsync(outboxMessage, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TranscriptionCompletionResult(
            TranscriptionCompletionStatus.Completed,
            payload.ConsultationId,
            transcript.Id);
    }
}
