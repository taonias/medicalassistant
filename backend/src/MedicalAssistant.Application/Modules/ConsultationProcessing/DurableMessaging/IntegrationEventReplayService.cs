using System.Text.Json;
using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Models.Messaging;

namespace MedicalAssistant.Application.Services;

public sealed class IntegrationEventReplayService
{
    private readonly IntegrationEventReplayPolicy _policy;
    private readonly IIntegrationEventReplayPublisher _publisher;
    private readonly IReadOnlyList<IIntegrationEventReplaySafetyCheck> _safetyChecks;

    public IntegrationEventReplayService(
        IntegrationEventReplayPolicy policy,
        IIntegrationEventReplayPublisher publisher,
        IEnumerable<IIntegrationEventReplaySafetyCheck>? safetyChecks = null)
    {
        _policy = policy;
        _publisher = publisher;
        _safetyChecks = safetyChecks?.ToArray() ?? [];
    }

    public SafeDeadLetteredIntegrationEventMetadata Inspect(DeadLetteredIntegrationEvent deadLetteredEvent) =>
        DeadLetteredIntegrationEventInspector.Inspect(deadLetteredEvent);

    public async Task<IntegrationEventReplayResult> ReplayAsync(
        IntegrationEventReplayCommand command,
        CancellationToken cancellationToken = default)
    {
        SafeDeadLetteredIntegrationEventMetadata metadata;
        try
        {
            metadata = Inspect(command.DeadLetteredEvent);
        }
        catch (JsonException)
        {
            metadata = new SafeDeadLetteredIntegrationEventMetadata(
                EventId: Guid.Empty,
                EventType: "unknown",
                EventVersion: 0,
                SubscriberName: command.DeadLetteredEvent.SubscriberName,
                AttemptCount: Math.Max(0, command.DeadLetteredEvent.AttemptCount),
                ConsultationId: null,
                CorrelationId: null,
                CausationId: null,
                FailureCategory: command.DeadLetteredEvent.FailureCategory,
                DeadLetteredAtUtc: command.DeadLetteredEvent.DeadLetteredAtUtc);

            return new IntegrationEventReplayResult(
                Replayed: false,
                FailureCode: "invalid-dead-letter-envelope",
                Metadata: metadata,
                AuditAction: "integration-event-replay-denied",
                AuditDetails: "failureCode=invalid-dead-letter-envelope");
        }

        var decision = _policy.Evaluate(new IntegrationEventReplayRequest(
            metadata.EventId,
            command.OperatorId,
            command.Reason,
            command.ReplacementPayloadJson));

        if (!decision.IsAuthorized)
        {
            return new IntegrationEventReplayResult(
                Replayed: false,
                FailureCode: decision.FailureCode,
                Metadata: metadata,
                AuditAction: decision.AuditAction,
                AuditDetails: decision.AuditDetails);
        }

        foreach (var safetyCheck in _safetyChecks)
        {
            var safetyDecision = await safetyCheck.CheckAsync(metadata, cancellationToken);
            if (!safetyDecision.IsSafe)
            {
                var failureCode = safetyDecision.FailureCode ?? "replay-safety-check-failed";
                return new IntegrationEventReplayResult(
                    Replayed: false,
                    FailureCode: failureCode,
                    Metadata: metadata,
                    AuditAction: "integration-event-replay-denied",
                    AuditDetails: $"failureCode={failureCode}; originalEventId={metadata.EventId:N}");
            }
        }

        await _publisher.PublishAsync(
            new IntegrationEventReplayMessage(
                metadata.EventId,
                metadata.EventType,
                metadata.CorrelationId,
                command.DeadLetteredEvent.EnvelopeJson),
            cancellationToken);

        return new IntegrationEventReplayResult(
            Replayed: true,
            FailureCode: null,
            Metadata: metadata,
            AuditAction: decision.AuditAction,
            AuditDetails: decision.AuditDetails);
    }
}
