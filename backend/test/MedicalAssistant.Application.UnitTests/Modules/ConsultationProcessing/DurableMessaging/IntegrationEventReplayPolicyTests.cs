using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Services;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.UnitTests.Features;

public class IntegrationEventReplayPolicyTests
{
    [Fact]
    public void Replay_requires_original_event_operator_and_reason_without_payload_override()
    {
        var policy = new IntegrationEventReplayPolicy();

        var decision = policy.Evaluate(new IntegrationEventReplayRequest(
            Guid.NewGuid(),
            "operator-1",
            "retry-after-speech-outage"));

        Assert.True(decision.IsAuthorized);
        Assert.Null(decision.FailureCode);
        Assert.Equal("integration-event-replay-approved", decision.AuditAction);
        Assert.Contains("operator=operator-1", decision.AuditDetails);
        Assert.Contains("reasonCode=retry-after-speech-outage", decision.AuditDetails);
    }

    [Fact]
    public void Replay_rejects_payload_editing_even_when_operator_and_reason_are_present()
    {
        var policy = new IntegrationEventReplayPolicy();

        var decision = policy.Evaluate(new IntegrationEventReplayRequest(
            Guid.NewGuid(),
            "operator-1",
            "retry-after-speech-outage",
            ReplacementPayloadJson: "{\"transcriptText\":\"edited patient text\"}"));

        Assert.False(decision.IsAuthorized);
        Assert.Equal("payload-editing-not-allowed", decision.FailureCode);
        Assert.DoesNotContain("edited patient text", decision.AuditDetails);
    }

    [Theory]
    [InlineData("", "reason", "operator-required")]
    [InlineData("operator-1", "", "reason-required")]
    public void Replay_rejects_missing_accountability_fields(
        string operatorId,
        string reason,
        string expectedFailure)
    {
        var policy = new IntegrationEventReplayPolicy();

        var decision = policy.Evaluate(new IntegrationEventReplayRequest(
            Guid.NewGuid(),
            operatorId,
            reason));

        Assert.False(decision.IsAuthorized);
        Assert.Equal(expectedFailure, decision.FailureCode);
    }

    [Fact]
    public void Retention_policy_requires_tombstones_to_outlive_event_records()
    {
        var validator = new EventRetentionOptionsValidator();

        var result = validator.Validate(Options.DefaultName, new EventRetentionOptions
        {
            OutboxRetention = TimeSpan.FromDays(30),
            InboxRetention = TimeSpan.FromDays(30),
            DeadLetterRetention = TimeSpan.FromDays(14),
            TombstoneRetention = TimeSpan.FromDays(7)
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("TombstoneRetention"));
    }
}
