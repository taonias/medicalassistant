using MedicalAssistant.EventBus;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqSubscriberReadinessHealthCheck : IHealthCheck
{
    private readonly RabbitMqTopologyOptions _topology;
    private readonly RabbitMqConsumerOptions _consumer;
    private readonly IntegrationEventSubscriptionRegistry _subscriptions;

    public RabbitMqSubscriberReadinessHealthCheck(
        IOptions<RabbitMqTopologyOptions> topology,
        IOptions<RabbitMqConsumerOptions> consumer,
        IntegrationEventSubscriptionRegistry subscriptions)
    {
        _topology = topology.Value;
        _consumer = consumer.Value;
        _subscriptions = subscriptions;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_topology.SubscriberName))
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ subscriber name is not configured."));

        if (string.IsNullOrWhiteSpace(_topology.QueueName))
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ topology queue name is not configured."));

        if (string.IsNullOrWhiteSpace(_consumer.QueueName))
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ consumer queue name is not configured."));

        if (_subscriptions.Subscriptions.Count == 0)
            return Task.FromResult(HealthCheckResult.Unhealthy("No integration-event subscriptions are registered."));

        return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ subscriber topology is configured."));
    }
}
