using System.Diagnostics.Metrics;

namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqDeliveryMetrics : IRabbitMqDeliveryObserver, IDisposable
{
    public const string MeterName = "MedicalAssistant.EventBusRabbitMQ";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _deliveriesHandled;
    private readonly Counter<long> _deliveriesRouted;

    public RabbitMqDeliveryMetrics()
    {
        _deliveriesHandled = _meter.CreateCounter<long>(
            "medicalassistant.eventbus.deliveries.handled",
            unit: "{delivery}",
            description: "RabbitMQ integration-event deliveries handled by outcome.");
        _deliveriesRouted = _meter.CreateCounter<long>(
            "medicalassistant.eventbus.deliveries.routed",
            unit: "{delivery}",
            description: "RabbitMQ integration-event deliveries routed to retry or dead-letter queues.");
    }

    public void DeliveryHandled(string eventType, RabbitMqDeliveryOutcome outcome) =>
        _deliveriesHandled.Add(
            1,
            new KeyValuePair<string, object?>("event.type", eventType),
            new KeyValuePair<string, object?>("outcome", outcome.ToString()));

    public void RoutedToRetry(string eventType, int retryAttempt) =>
        _deliveriesRouted.Add(
            1,
            new KeyValuePair<string, object?>("event.type", eventType),
            new KeyValuePair<string, object?>("queue.role", "retry"),
            new KeyValuePair<string, object?>("retry.attempt", retryAttempt));

    public void RoutedToDeadLetter(string eventType) =>
        _deliveriesRouted.Add(
            1,
            new KeyValuePair<string, object?>("event.type", eventType),
            new KeyValuePair<string, object?>("queue.role", "dead-letter"));

    public void Dispose() => _meter.Dispose();
}
