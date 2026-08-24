namespace MedicalAssistant.Application.Services;

public sealed class IntegrationEventReplayPolicy
{
    public IntegrationEventReplayDecision Evaluate(IntegrationEventReplayRequest request)
    {
        if (request.OriginalEventId == Guid.Empty)
        {
            return IntegrationEventReplayDecision.Denied("original-event-id-required");
        }

        if (string.IsNullOrWhiteSpace(request.OperatorId))
        {
            return IntegrationEventReplayDecision.Denied("operator-required");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return IntegrationEventReplayDecision.Denied("reason-required");
        }

        if (!string.IsNullOrWhiteSpace(request.ReplacementPayloadJson))
        {
            return IntegrationEventReplayDecision.Denied("payload-editing-not-allowed");
        }

        return IntegrationEventReplayDecision.Approved(
            "integration-event-replay-approved",
            $"operator={request.OperatorId.Trim()}; originalEventId={request.OriginalEventId:N}; reasonCode={request.Reason.Trim()}");
    }
}

public sealed record IntegrationEventReplayRequest(
    Guid OriginalEventId,
    string OperatorId,
    string Reason,
    string? ReplacementPayloadJson = null);

public sealed record IntegrationEventReplayDecision(
    bool IsAuthorized,
    string? FailureCode,
    string AuditAction,
    string AuditDetails)
{
    public static IntegrationEventReplayDecision Approved(string auditAction, string auditDetails) =>
        new(true, null, auditAction, auditDetails);

    public static IntegrationEventReplayDecision Denied(string failureCode) =>
        new(false, failureCode, "integration-event-replay-denied", $"failureCode={failureCode}");
}
