using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Models.Messaging;
using MedicalAssistant.Application.Services;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;

namespace MedicalAssistant.Application.UnitTests.Features;

public class IntegrationEventReplayServiceTests
{
    [Fact]
    public async Task Replay_publishes_the_original_immutable_dead_letter_envelope_after_operator_approval()
    {
        var publisher = new RecordingReplayPublisher();
        var service = new IntegrationEventReplayService(new IntegrationEventReplayPolicy(), publisher);
        const int consultationId = 456;
        var eventId = Guid.NewGuid();
        var envelopeJson = CreateEnvelopeJson(eventId, consultationId, "contains patient transcript canary");

        var result = await service.ReplayAsync(new IntegrationEventReplayCommand(
            new DeadLetteredIntegrationEvent(
                SubscriberName: "transcription-worker",
                EnvelopeJson: envelopeJson,
                AttemptCount: 5,
                FailureCategory: "TransientDependency",
                DeadLetteredAtUtc: DateTimeOffset.Parse("2026-08-04T10:15:00Z")),
            OperatorId: "operator-1",
            Reason: "retry-after-speech-outage"));

        Assert.True(result.Replayed);
        Assert.Null(result.FailureCode);
        Assert.NotNull(publisher.Message);
        Assert.Equal(eventId, publisher.Message.EventId);
        Assert.Equal(ConsultationIntegrationEvents.AudioUploadedV1, publisher.Message.EventType);
        Assert.Equal(envelopeJson, publisher.Message.EnvelopeJson);
        Assert.Equal(consultationId.ToString(), result.Metadata.ConsultationId);
        Assert.Equal(5, result.Metadata.AttemptCount);
        Assert.Equal("TransientDependency", result.Metadata.FailureCategory);
        Assert.DoesNotContain("contains patient transcript canary", result.AuditDetails);
    }

    [Fact]
    public async Task Replay_denies_payload_editing_and_does_not_publish()
    {
        var publisher = new RecordingReplayPublisher();
        var service = new IntegrationEventReplayService(new IntegrationEventReplayPolicy(), publisher);

        var result = await service.ReplayAsync(new IntegrationEventReplayCommand(
            new DeadLetteredIntegrationEvent(
                SubscriberName: "transcription-worker",
                EnvelopeJson: CreateEnvelopeJson(Guid.NewGuid(), 457, "original clinical text"),
                AttemptCount: 5),
            OperatorId: "operator-1",
            Reason: "retry-after-speech-outage",
            ReplacementPayloadJson: "{\"text\":\"edited patient payload\"}"));

        Assert.False(result.Replayed);
        Assert.Equal("payload-editing-not-allowed", result.FailureCode);
        Assert.Null(publisher.Message);
        Assert.DoesNotContain("edited patient payload", result.AuditDetails);
    }

    [Fact]
    public async Task Replay_denies_when_a_safety_check_rejects_current_state_or_contract()
    {
        var publisher = new RecordingReplayPublisher();
        var service = new IntegrationEventReplayService(
            new IntegrationEventReplayPolicy(),
            publisher,
            [new RejectingReplaySafetyCheck("unsupported-event-contract")]);

        var result = await service.ReplayAsync(new IntegrationEventReplayCommand(
            new DeadLetteredIntegrationEvent(
                SubscriberName: "transcription-worker",
                EnvelopeJson: CreateEnvelopeJson(Guid.NewGuid(), 459, "original clinical text"),
                AttemptCount: 5),
            OperatorId: "operator-1",
            Reason: "retry-after-speech-outage"));

        Assert.False(result.Replayed);
        Assert.Equal("unsupported-event-contract", result.FailureCode);
        Assert.Null(publisher.Message);
        Assert.Contains("unsupported-event-contract", result.AuditDetails);
    }

    [Fact]
    public void Inspect_returns_only_safe_dead_letter_metadata()
    {
        var service = new IntegrationEventReplayService(
            new IntegrationEventReplayPolicy(),
            new RecordingReplayPublisher());
        const int consultationId = 458;

        var metadata = service.Inspect(new DeadLetteredIntegrationEvent(
            SubscriberName: "backend-clinical-knowledge",
            EnvelopeJson: CreateEnvelopeJson(Guid.NewGuid(), consultationId, "clinical payload should stay protected"),
            AttemptCount: 3,
            FailureCategory: "ContractUnsupported"));

        Assert.Equal("backend-clinical-knowledge", metadata.SubscriberName);
        Assert.Equal(consultationId.ToString(), metadata.ConsultationId);
        Assert.Equal("ContractUnsupported", metadata.FailureCategory);
        Assert.DoesNotContain(
            "clinical payload should stay protected",
            string.Join(";", metadata.GetType().GetProperties().Select(property => property.GetValue(metadata)?.ToString())));
    }

    private static string CreateEnvelopeJson(
        Guid eventId,
        int consultationId,
        string protectedPayloadValue)
    {
        var envelope = new IntegrationEventEnvelope<ConsultationAudioUploadedV1>(
            eventId,
            ConsultationIntegrationEvents.AudioUploadedV1,
            1,
            DateTime.UtcNow,
            "medicalassistant.backend",
            "correlation-1",
            "causation-1",
            new ConsultationAudioUploadedV1(
                consultationId,
                "file-1",
                "audio/wav",
                protectedPayloadValue,
                123));

        return IntegrationEventSerializer.Serialize(envelope);
    }

    private sealed class RecordingReplayPublisher : IIntegrationEventReplayPublisher
    {
        public IntegrationEventReplayMessage? Message { get; private set; }

        public Task PublishAsync(
            IntegrationEventReplayMessage message,
            CancellationToken cancellationToken = default)
        {
            Message = message;
            return Task.CompletedTask;
        }
    }

    private sealed class RejectingReplaySafetyCheck : IIntegrationEventReplaySafetyCheck
    {
        private readonly string _failureCode;

        public RejectingReplaySafetyCheck(string failureCode)
        {
            _failureCode = failureCode;
        }

        public Task<IntegrationEventReplaySafetyDecision> CheckAsync(
            SafeDeadLetteredIntegrationEventMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IntegrationEventReplaySafetyDecision.Unsafe(_failureCode));
        }
    }
}
