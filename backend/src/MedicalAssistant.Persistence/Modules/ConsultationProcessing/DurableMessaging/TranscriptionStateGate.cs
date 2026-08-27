using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus.Contracts;

namespace MedicalAssistant.Persistence.Modules.ConsultationProcessing.DurableMessaging;

internal static class TranscriptionStateGate
{
    public const string FailureCategory = "StateGate";
    public const string DeletedFailureCode = "consultation-deleted";
    public const string SupersededFailureCode = "consultation-source-superseded";

    public static TranscriptionStateGateResult? Evaluate(
        Consultation consultation,
        ConsultationAudioUploadedV1 payload)
    {
        if (consultation.DeletedAtUtc is not null || consultation.Status == ConsultationStatus.Deleted)
        {
            return new TranscriptionStateGateResult(
                TranscriptionInboxClaimStatus.SkippedDeleted,
                TranscriptionCompletionStatus.IgnoredDeleted,
                TranscriptionFailureStatus.IgnoredDeleted,
                DeletedFailureCode);
        }

        if (!string.Equals(
                consultation.SourceObjectReference,
                payload.StorageObjectReference,
                StringComparison.Ordinal))
        {
            return new TranscriptionStateGateResult(
                TranscriptionInboxClaimStatus.SkippedSuperseded,
                TranscriptionCompletionStatus.IgnoredSuperseded,
                TranscriptionFailureStatus.IgnoredSuperseded,
                SupersededFailureCode);
        }

        return null;
    }
}

internal sealed record TranscriptionStateGateResult(
    TranscriptionInboxClaimStatus InboxClaimStatus,
    TranscriptionCompletionStatus CompletionStatus,
    TranscriptionFailureStatus FailureStatus,
    string FailureCode);
