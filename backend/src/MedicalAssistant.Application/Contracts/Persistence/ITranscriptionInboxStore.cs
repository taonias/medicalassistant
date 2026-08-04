using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface ITranscriptionInboxStore
{
    Task<TranscriptionInboxClaimResult> ClaimAsync(
        string consumerName,
        IntegrationEventEnvelope<ConsultationAudioUploadedV1> envelope,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);
}

public sealed record TranscriptionInboxClaimResult(
    TranscriptionInboxClaimStatus Status);

public enum TranscriptionInboxClaimStatus
{
    Claimed = 0,
    DuplicateCompleted = 1,
    ActiveInProgress = 2,
    SkippedDeleted = 3,
    SkippedSuperseded = 4
}
