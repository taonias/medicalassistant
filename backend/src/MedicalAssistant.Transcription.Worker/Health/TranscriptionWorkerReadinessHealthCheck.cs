using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcription.Worker.Health;

public sealed class TranscriptionWorkerReadinessHealthCheck : IHealthCheck
{
    private readonly RabbitMqTopologyOptions _topology;
    private readonly RabbitMqConsumerOptions _consumer;
    private readonly IntegrationEventSubscriptionRegistry _subscriptions;

    public TranscriptionWorkerReadinessHealthCheck(
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

        var hasAudioSubscription = _subscriptions.Subscriptions.Any(subscription =>
            subscription.Contract.EventType == ConsultationIntegrationEvents.AudioUploadedV1);
        if (!hasAudioSubscription)
            return Task.FromResult(HealthCheckResult.Unhealthy("Audio Uploaded subscription is not registered."));

        return Task.FromResult(HealthCheckResult.Healthy("Transcription worker configuration is ready."));
    }
}
