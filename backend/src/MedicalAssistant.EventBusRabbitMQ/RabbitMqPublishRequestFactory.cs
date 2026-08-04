using System.Diagnostics;
using System.Text;
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

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            MessageId = envelope.EventId.ToString("N"),
            CorrelationId = envelope.CorrelationId,
            Type = envelope.EventType,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = BuildTraceHeaders()
        };
        var body = Encoding.UTF8.GetBytes(IntegrationEventSerializer.Serialize(envelope));

        return new RabbitMqPublishRequest(
            options.ExchangeName,
            envelope.EventType,
            Mandatory: true,
            properties,
            body);
    }

    private static Dictionary<string, object?> BuildTraceHeaders()
    {
        var headers = new Dictionary<string, object?>(StringComparer.Ordinal);
        var activity = Activity.Current;
        if (activity?.Id is not null)
        {
            headers["traceparent"] = activity.Id;
        }

        if (!string.IsNullOrWhiteSpace(activity?.TraceStateString))
        {
            headers["tracestate"] = activity.TraceStateString;
        }

        return headers;
    }
}
