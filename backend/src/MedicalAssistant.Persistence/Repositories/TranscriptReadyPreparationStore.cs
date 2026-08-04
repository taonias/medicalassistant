using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Repositories;

public sealed class TranscriptReadyPreparationStore : ITranscriptReadyPreparationStore
{
    private const string FailureCategory = "TranscriptReadyStateGate";
    private const string DeletedFailureCode = "consultation-deleted";
    private const string SupersededFailureCode = "transcript-revision-superseded";
    private const string UnauthorizedFailureCode = "patient-doctor-mismatch";

    private readonly MedicalAssistantDatabaseContext _context;

    public TranscriptReadyPreparationStore(MedicalAssistantDatabaseContext context)
    {
        _context = context;
    }

    public async Task<TranscriptReadyPreparationResult> PrepareAsync(
        string consumerName,
        IntegrationEventEnvelope<ConsultationTranscriptReadyV1> envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);

        var payload = envelope.Payload;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var inbox = await _context.ConsultationInboxMessages
            .SingleOrDefaultAsync(message =>
                    message.ConsumerName == consumerName &&
                    message.EventId == envelope.EventId,
                cancellationToken);

        if (inbox?.Status == ConsultationEventMessageStatus.Completed)
        {
            await transaction.CommitAsync(cancellationToken);
            return new TranscriptReadyPreparationResult(
                TranscriptReadyPreparationStatus.DuplicateCompleted,
                Request: null);
        }

        inbox ??= new ConsultationInboxMessage
        {
            ConsumerName = consumerName,
            EventId = envelope.EventId,
            EventType = envelope.EventType,
            EventVersion = envelope.EventVersion,
            ReceivedAtUtc = now
        };
        if (inbox.Id == 0)
        {
            await _context.ConsultationInboxMessages.AddAsync(inbox, cancellationToken);
        }

        var consultation = await _context.Consultations
            .SingleOrDefaultAsync(c => c.Id == payload.ConsultationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Consultation), payload.ConsultationId);

        if (consultation.DeletedAtUtc is not null || consultation.Status == ConsultationStatus.Deleted)
        {
            await CompleteInboxAndCommitAsync(
                inbox,
                now,
                DeletedFailureCode,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new TranscriptReadyPreparationResult(
                TranscriptReadyPreparationStatus.IgnoredDeleted,
                Request: null);
        }

        var transcript = await _context.Transcripts
            .SingleOrDefaultAsync(t =>
                    t.Id == payload.TranscriptId &&
                    t.ConsultationId == payload.ConsultationId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Transcript), payload.TranscriptId);

        if (transcript.Status != TranscriptStatus.Completed ||
            transcript.Revision != payload.TranscriptRevision ||
            string.IsNullOrWhiteSpace(transcript.TranscriptText))
        {
            await CompleteInboxAndCommitAsync(
                inbox,
                now,
                SupersededFailureCode,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new TranscriptReadyPreparationResult(
                TranscriptReadyPreparationStatus.IgnoredSuperseded,
                Request: null);
        }

        Patient? patient = null;
        if (consultation.PatientId is not null)
        {
            patient = await _context.Patients
                .SingleOrDefaultAsync(p => p.Id == consultation.PatientId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Patient), consultation.PatientId.Value);

            if (!patient.BelongsToDoctor(consultation.DoctorId))
            {
                await CompleteInboxAndCommitAsync(
                    inbox,
                    now,
                    UnauthorizedFailureCode,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new TranscriptReadyPreparationResult(
                    TranscriptReadyPreparationStatus.IgnoredUnauthorized,
                    Request: null);
            }
        }

        MarkInboxCompleted(inbox, now, failureCode: null);
        consultation.MarkStructuredDataPending();
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var correlationId = string.IsNullOrWhiteSpace(envelope.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : envelope.CorrelationId;
        var request = new PreparedSessionTranscriptRequest(
            consultation.Id,
            transcript.Id,
            transcript.Revision,
            consultation.PatientId,
            patient?.ExternalPatientId,
            patient is null ? null : $"{patient.FirstName} {patient.LastName}",
            consultation.DoctorId,
            consultation.ConsultationDate,
            payload.LanguageCode,
            transcript.TranscriptText,
            correlationId);

        return new TranscriptReadyPreparationResult(
            TranscriptReadyPreparationStatus.Prepared,
            request);
    }

    private async Task CompleteInboxAndCommitAsync(
        ConsultationInboxMessage inbox,
        DateTime completedAtUtc,
        string failureCode,
        CancellationToken cancellationToken)
    {
        MarkInboxCompleted(inbox, completedAtUtc, failureCode);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void MarkInboxCompleted(
        ConsultationInboxMessage inbox,
        DateTime completedAtUtc,
        string? failureCode)
    {
        inbox.Status = ConsultationEventMessageStatus.Completed;
        inbox.AttemptCount++;
        inbox.LastAttemptAtUtc = completedAtUtc;
        inbox.CompletedAtUtc = completedAtUtc;
        inbox.LeaseOwner = null;
        inbox.LeaseExpiresAtUtc = null;
        inbox.LastFailureCategory = failureCode is null ? null : FailureCategory;
        inbox.LastFailureCode = failureCode;
    }
}
