using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MedicalAssistant.EventBus;
using RabbitMQ.Client;

namespace MedicalAssistant.EventBusRabbitMQ;

public static class RabbitMqPublishRequestFactory
{
    public static RabbitMqPublishRequest Create<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        RabbitMqPublishOptions options)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(options);

        var properties = CreateProperties(envelope.EventId, envelope.EventType, envelope.CorrelationId);
        var body = Encoding.UTF8.GetBytes(IntegrationEventSerializer.Serialize(envelope));

        return new RabbitMqPublishRequest(
            options.ExchangeName,
            envelope.EventType,
            Mandatory: true,
            properties,
            body);
    }

    public static RabbitMqPublishRequest CreateFromOutbox(
        Guid eventId,
        string eventType,
        int eventVersion,
        DateTime occurredAtUtc,
        string producer,
        string? correlationId,
        string? causationId,
        string payloadJson,
        RabbitMqPublishOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(producer);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);
        ArgumentNullException.ThrowIfNull(options);

        using var payload = JsonDocument.Parse(payloadJson);
        var envelopeJson = JsonSerializer.Serialize(
            new StoredEnvelope(
                eventId,
                eventType,
                eventVersion,
                occurredAtUtc,
                producer,
                correlationId,
                causationId,
                payload.RootElement),
            IntegrationEventSerializer.Options);

        return new RabbitMqPublishRequest(
            options.ExchangeName,
            eventType,
            Mandatory: true,
            CreateProperties(eventId, eventType, correlationId),
            Encoding.UTF8.GetBytes(envelopeJson));
    }

    public static RabbitMqPublishRequest CreateReplay(
        Guid eventId,
        string eventType,
        string? correlationId,
        string envelopeJson,
        RabbitMqPublishOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(eventId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(envelopeJson);
        ArgumentNullException.ThrowIfNull(options);

        return new RabbitMqPublishRequest(
            options.ExchangeName,
            eventType,
            Mandatory: true,
            CreateProperties(eventId, eventType, correlationId),
            Encoding.UTF8.GetBytes(envelopeJson));
    }

    private static BasicProperties CreateProperties(
        Guid eventId,
        string eventType,
        string? correlationId) =>
        new()
        {
            Persistent = true,
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            MessageId = eventId.ToString("N"),
            CorrelationId = correlationId,
            Type = eventType,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = BuildTraceHeaders()
        };

    private static Dictionary<string, object?> BuildTraceHeaders()
    {
        var headers = new Dictionary<string, object?>(StringComparer.Ordinal);
        var activity = Activity.Current;
        if (activity?.Id is not null)
        {
            headers[RabbitMqTelemetryHeaders.TraceParent] = activity.Id;
        }

        if (!string.IsNullOrWhiteSpace(activity?.TraceStateString))
        {
            headers[RabbitMqTelemetryHeaders.TraceState] = activity.TraceStateString;
        }

        return headers;
    }

    private sealed record StoredEnvelope(
        Guid EventId,
        string EventType,
        int EventVersion,
        DateTime OccurredAtUtc,
        string Producer,
        string? CorrelationId,
        string? CausationId,
        JsonElement Payload);
}
