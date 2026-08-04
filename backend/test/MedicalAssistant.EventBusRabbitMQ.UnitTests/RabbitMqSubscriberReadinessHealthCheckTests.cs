using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class RabbitMqSubscriberReadinessHealthCheckTests
{
    [Fact]
    public async Task Readiness_is_healthy_when_queue_identity_and_subscriptions_are_configured()
    {
        var check = new RabbitMqSubscriberReadinessHealthCheck(
            Options.Create(new RabbitMqTopologyOptions
            {
                SubscriberName = "transcription-worker",
                QueueName = "medicalassistant.transcription-worker"
            }),
            Options.Create(new RabbitMqConsumerOptions
            {
                QueueName = "medicalassistant.transcription-worker"
            }),
            IntegrationEventSubscriptionRegistry.Create(
                ConsultationIntegrationEvents.Registry,
                builder => builder.Subscribe<ConsultationAudioUploadedV1, AudioUploadedHandler>()));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Readiness_reports_degraded_configuration_without_touching_remote_broker()
    {
        var check = new RabbitMqSubscriberReadinessHealthCheck(
            Options.Create(new RabbitMqTopologyOptions
            {
                SubscriberName = "transcription-worker",
                QueueName = ""
            }),
            Options.Create(new RabbitMqConsumerOptions
            {
                QueueName = "medicalassistant.transcription-worker"
            }),
            IntegrationEventSubscriptionRegistry.Create(
                ConsultationIntegrationEvents.Registry,
                _ => { }));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("topology queue name", result.Description);
    }

    private sealed class AudioUploadedHandler : IIntegrationEventHandler<ConsultationAudioUploadedV1>
    {
        public Task HandleAsync(
            IntegrationEventEnvelope<ConsultationAudioUploadedV1> envelope,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
