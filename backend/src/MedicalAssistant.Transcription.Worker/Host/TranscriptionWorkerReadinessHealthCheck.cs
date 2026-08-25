using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcription.Worker.Health;

public sealed class TranscriptionWorkerReadinessHealthCheck : IHealthCheck
{
    private readonly RabbitMqSubscriberReadinessHealthCheck _subscriberReadiness;
    private readonly IntegrationEventSubscriptionRegistry _subscriptions;

    public TranscriptionWorkerReadinessHealthCheck(
        IOptions<RabbitMqTopologyOptions> topology,
        IOptions<RabbitMqConsumerOptions> consumer,
        IntegrationEventSubscriptionRegistry subscriptions)
    {
        _subscriberReadiness = new RabbitMqSubscriberReadinessHealthCheck(topology, consumer, subscriptions);
        _subscriptions = subscriptions;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var subscriberReadiness = await _subscriberReadiness.CheckHealthAsync(context, cancellationToken);
        if (subscriberReadiness.Status != HealthStatus.Healthy)
            return subscriberReadiness;

        var hasAudioSubscription = _subscriptions.Subscriptions.Any(subscription =>
            subscription.Contract.EventType == ConsultationIntegrationEvents.AudioUploadedV1);
        if (!hasAudioSubscription)
            return HealthCheckResult.Unhealthy("Audio Uploaded subscription is not registered.");

        return HealthCheckResult.Healthy("Transcription worker configuration is ready.");
    }
}
