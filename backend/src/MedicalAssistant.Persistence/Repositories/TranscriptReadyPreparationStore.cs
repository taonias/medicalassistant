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

        if (consultation.PatientId is null)
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

        var patient = await _context.Patients
            .SingleOrDefaultAsync(p => p.Id == consultation.PatientId.Value, cancellationToken);
        if (patient is null)
        {
            throw new NotFoundException(nameof(Patient), consultation.PatientId.Value);
        }

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

        MarkInboxInProgress(inbox, now);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var correlationId = string.IsNullOrWhiteSpace(envelope.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : envelope.CorrelationId;
        var request = new PreparedSessionTranscriptRequest(
            consultation.Id,
            transcript.Id,
            transcript.Revision,
            consultation.PatientId.Value,
            patient.ExternalPatientId,
            $"{patient.FirstName} {patient.LastName}",
            consultation.DoctorId,
            consultation.ConsultationDate,
            payload.LanguageCode,
            transcript.TranscriptText,
            correlationId);

        return new TranscriptReadyPreparationResult(
            TranscriptReadyPreparationStatus.Prepared,
            request);
    }

    public async Task CompleteAcceptedAsync(
        string consumerName,
        IntegrationEventEnvelope<ConsultationTranscriptReadyV1> envelope,
        TranscriptReadyAcceptedResult accepted,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(accepted.DocumentId);

        var now = DateTime.UtcNow;
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var inbox = await _context.ConsultationInboxMessages
            .SingleOrDefaultAsync(message =>
                    message.ConsumerName == consumerName &&
                    message.EventId == envelope.EventId,
                cancellationToken);

        if (inbox?.Status == ConsultationEventMessageStatus.Completed)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (inbox is null)
        {
            inbox = new ConsultationInboxMessage
            {
                ConsumerName = consumerName,
                EventId = envelope.EventId,
                EventType = envelope.EventType,
                EventVersion = envelope.EventVersion,
                ReceivedAtUtc = now,
                AttemptCount = 1,
                LastAttemptAtUtc = now
            };
            await _context.ConsultationInboxMessages.AddAsync(inbox, cancellationToken);
        }

        var consultation = await _context.Consultations
            .SingleOrDefaultAsync(c => c.Id == envelope.Payload.ConsultationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Consultation), envelope.Payload.ConsultationId);

        consultation.MarkStructuredDataPending();
        MarkInboxCompleted(inbox, now, failureCode: null);
        inbox.ClinicalKnowledgeIngestionId = accepted.IngestionId;
        inbox.ClinicalKnowledgeDocumentId = accepted.DocumentId;
        inbox.ClinicalKnowledgeDuplicate = accepted.Duplicate;

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

    private static void MarkInboxInProgress(
        ConsultationInboxMessage inbox,
        DateTime attemptedAtUtc)
    {
        inbox.Status = ConsultationEventMessageStatus.InProgress;
        inbox.AttemptCount++;
        inbox.LastAttemptAtUtc = attemptedAtUtc;
        inbox.CompletedAtUtc = null;
        inbox.LeaseOwner = null;
        inbox.LeaseExpiresAtUtc = null;
        inbox.LastFailureCategory = null;
        inbox.LastFailureCode = null;
    }
}
