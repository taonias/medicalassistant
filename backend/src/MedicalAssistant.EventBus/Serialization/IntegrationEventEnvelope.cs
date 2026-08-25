using System.Text.Json.Serialization;

namespace MedicalAssistant.EventBus;

public sealed record IntegrationEventEnvelope<TPayload>(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("eventVersion")] int EventVersion,
    [property: JsonPropertyName("occurredAtUtc")] DateTime OccurredAtUtc,
    [property: JsonPropertyName("producer")] string Producer,
    [property: JsonPropertyName("correlationId")] string? CorrelationId,
    [property: JsonPropertyName("causationId")] string? CausationId,
    [property: JsonPropertyName("payload")] TPayload Payload);

public static class IntegrationEventEnvelope
{
    public static IntegrationEventEnvelope<TPayload> Create<TPayload>(
        TPayload payload,
        IntegrationEventContractDescriptor descriptor,
        string producer,
        string? correlationId,
        string? causationId,
        DateTime? occurredAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(producer);

        if (descriptor.ClrType != typeof(TPayload))
        {
            throw new UnsupportedIntegrationEventContractException(
                $"Descriptor for '{descriptor.EventType}' does not describe payload type '{typeof(TPayload).FullName}'.");
        }

        return new IntegrationEventEnvelope<TPayload>(
            EventId: Guid.NewGuid(),
            EventType: descriptor.EventType,
            EventVersion: descriptor.EventVersion,
            OccurredAtUtc: occurredAtUtc ?? DateTime.UtcNow,
            Producer: producer,
            CorrelationId: correlationId,
            CausationId: causationId,
            Payload: payload);
    }
}
