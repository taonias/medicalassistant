using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Modules.ConsultationProcessing.DurableMessaging;

public sealed class TranscriptionInboxStore : ITranscriptionInboxStore
{
    private readonly MedicalAssistantDatabaseContext _context;

    public TranscriptionInboxStore(MedicalAssistantDatabaseContext context)
    {
        _context = context;
    }

    public async Task<TranscriptionInboxClaimResult> ClaimAsync(
        string consumerName,
        IntegrationEventEnvelope<ConsultationAudioUploadedV1> envelope,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);

        var now = DateTime.UtcNow;
        var inbox = await _context.ConsultationInboxMessages
            .SingleOrDefaultAsync(message =>
                    message.ConsumerName == consumerName &&
                    message.EventId == envelope.EventId,
                cancellationToken);

        if (inbox?.Status == ConsultationEventMessageStatus.Completed)
            return new TranscriptionInboxClaimResult(TranscriptionInboxClaimStatus.DuplicateCompleted);

        if (inbox?.Status == ConsultationEventMessageStatus.InProgress &&
            inbox.LeaseExpiresAtUtc > now)
            return new TranscriptionInboxClaimResult(TranscriptionInboxClaimStatus.ActiveInProgress);

        var consultation = await _context.Consultations
            .SingleOrDefaultAsync(c => c.Id == envelope.Payload.ConsultationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Consultation), envelope.Payload.ConsultationId);

        var stateGate = TranscriptionStateGate.Evaluate(consultation, envelope.Payload);
        if (stateGate is not null)
        {
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

            inbox.Status = ConsultationEventMessageStatus.Completed;
            inbox.AttemptCount++;
            inbox.LastAttemptAtUtc = now;
            inbox.CompletedAtUtc = now;
            inbox.LeaseOwner = null;
            inbox.LeaseExpiresAtUtc = null;
            inbox.LastFailureCategory = TranscriptionStateGate.FailureCategory;
            inbox.LastFailureCode = stateGate.FailureCode;

            await _context.SaveChangesAsync(cancellationToken);
            return new TranscriptionInboxClaimResult(stateGate.InboxClaimStatus);
        }

        if (inbox is null)
        {
            inbox = new ConsultationInboxMessage
            {
                ConsumerName = consumerName,
                EventId = envelope.EventId,
                EventType = envelope.EventType,
                EventVersion = envelope.EventVersion,
                ReceivedAtUtc = now
            };
            await _context.ConsultationInboxMessages.AddAsync(inbox, cancellationToken);
        }

        inbox.Status = ConsultationEventMessageStatus.InProgress;
        inbox.AttemptCount++;
        inbox.LastAttemptAtUtc = now;
        inbox.LeaseOwner = leaseOwner;
        inbox.LeaseExpiresAtUtc = now.Add(leaseDuration);
        inbox.LastFailureCategory = null;
        inbox.LastFailureCode = null;

        await _context.SaveChangesAsync(cancellationToken);
        return new TranscriptionInboxClaimResult(TranscriptionInboxClaimStatus.Claimed);
    }
}
