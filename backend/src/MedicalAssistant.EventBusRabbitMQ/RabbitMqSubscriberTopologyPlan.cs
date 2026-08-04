using MedicalAssistant.EventBus;

namespace MedicalAssistant.EventBusRabbitMQ;

public sealed record RabbitMqQueuePlan(
    string Name,
    string? DeadLetterExchange = null,
    string? DeadLetterRoutingKey = null,
    TimeSpan? MessageTtl = null);

public sealed record RabbitMqBindingPlan(
    string QueueName,
    string RoutingKey);

public sealed record RabbitMqSubscriberTopologyPlan(
    string ExchangeName,
    RabbitMqQueuePlan MainQueue,
    IReadOnlyList<RabbitMqQueuePlan> RetryQueues,
    RabbitMqQueuePlan DeadLetterQueue,
    IReadOnlyList<RabbitMqBindingPlan> Bindings)
{
    public static RabbitMqSubscriberTopologyPlan Create(
        RabbitMqTopologyOptions options,
        IntegrationEventSubscriptionRegistry subscriptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.QueueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SubscriberName);

        var eventTypes = subscriptions.Subscriptions
            .Select(subscription => subscription.Contract.EventType)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var deadLetterQueue = new RabbitMqQueuePlan($"{options.QueueName}.dlq");
        var retryQueues = options.RetryDelays
            .Select((delay, index) => new RabbitMqQueuePlan(
                Name: $"{options.QueueName}.retry.{index + 1}",
                DeadLetterExchange: options.ExchangeName,
                DeadLetterRoutingKey: eventTypes.SingleOrDefault(),
                MessageTtl: delay))
            .ToArray();
        var bindings = eventTypes
            .Select(eventType => new RabbitMqBindingPlan(options.QueueName, eventType))
            .ToArray();

        return new RabbitMqSubscriberTopologyPlan(
            ExchangeName: options.ExchangeName,
            MainQueue: new RabbitMqQueuePlan(
                Name: options.QueueName,
                DeadLetterExchange: string.Empty,
                DeadLetterRoutingKey: deadLetterQueue.Name),
            RetryQueues: retryQueues,
            DeadLetterQueue: deadLetterQueue,
            Bindings: bindings);
    }
}
