using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;

namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class RabbitMqSubscriberTopologyTests
{
    [Fact]
    public void Topology_plan_uses_a_single_live_queue_plus_a_dead_letter_queue_by_default()
    {
        var subscriptions = IntegrationEventSubscriptionRegistry.Create(
            ConsultationIntegrationEvents.Registry,
            builder => builder.Subscribe<ConsultationAudioUploadedV1, AudioUploadedHandler>());
        var options = new RabbitMqTopologyOptions
        {
            SubscriberName = "transcription-worker",
            QueueName = "medicalassistant.transcription.q"
        };

        var plan = RabbitMqSubscriberTopologyPlan.Create(options, subscriptions);

        Assert.Equal("medicalassistant.events", plan.ExchangeName);
        Assert.Equal("medicalassistant.transcription.q", plan.MainQueue.Name);
        Assert.Equal("medicalassistant.transcription.q.dlq", plan.DeadLetterQueue.Name);
        // Automatic retries are disabled by default: no delayed-retry queues.
        Assert.Empty(plan.RetryQueues);
        // The main queue still dead-letters to its dlq, which is the safety net for poison messages.
        Assert.Equal("medicalassistant.transcription.q.dlq", plan.MainQueue.DeadLetterRoutingKey);
        Assert.Single(plan.Bindings);
        Assert.Equal(ConsultationIntegrationEvents.AudioUploadedV1, plan.Bindings[0].RoutingKey);
        Assert.Equal("medicalassistant.transcription.q", plan.Bindings[0].QueueName);
    }

    [Fact]
    public void Retry_queues_are_only_created_when_retry_delays_are_explicitly_configured()
    {
        var subscriptions = IntegrationEventSubscriptionRegistry.Create(
            ConsultationIntegrationEvents.Registry,
            builder => builder.Subscribe<ConsultationAudioUploadedV1, AudioUploadedHandler>());
        var plan = RabbitMqSubscriberTopologyPlan.Create(
            new RabbitMqTopologyOptions
            {
                QueueName = "medicalassistant.transcription.q",
                SubscriberName = "transcription-worker",
                RetryDelays = [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1)]
            },
            subscriptions);

        // The delayed-retry mechanism is retained but off by default; configuring delays re-creates it.
        Assert.Equal(2, plan.RetryQueues.Count);
        Assert.All(plan.RetryQueues, queue =>
        {
            Assert.StartsWith("medicalassistant.transcription.q.retry.", queue.Name);
            Assert.Equal("medicalassistant.events", queue.DeadLetterExchange);
            Assert.Equal(ConsultationIntegrationEvents.AudioUploadedV1, queue.DeadLetterRoutingKey);
            Assert.True(queue.MessageTtl > TimeSpan.Zero);
        });
    }

    [Fact]
    public void Future_document_processor_topology_binds_only_document_uploaded_events()
    {
        var subscriptions = IntegrationEventSubscriptionRegistry.Create(
            ConsultationIntegrationEvents.Registry,
            builder => builder.Subscribe<ConsultationDocumentUploadedV1, DocumentUploadedHandler>());
        var options = new RabbitMqTopologyOptions
        {
            SubscriberName = "document-processor",
            QueueName = "medicalassistant.document-processing.q"
        };

        var plan = RabbitMqSubscriberTopologyPlan.Create(options, subscriptions);

        Assert.Equal("medicalassistant.events", plan.ExchangeName);
        Assert.Equal("medicalassistant.document-processing.q", plan.MainQueue.Name);
        Assert.Single(plan.Bindings);
        Assert.Equal(ConsultationIntegrationEvents.DocumentUploadedV1, plan.Bindings[0].RoutingKey);
        Assert.Equal("medicalassistant.document-processing.q", plan.Bindings[0].QueueName);
    }

    private sealed class AudioUploadedHandler : IIntegrationEventHandler<ConsultationAudioUploadedV1>
    {
        public Task HandleAsync(
            IntegrationEventEnvelope<ConsultationAudioUploadedV1> envelope,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class DocumentUploadedHandler : IIntegrationEventHandler<ConsultationDocumentUploadedV1>
    {
        public Task HandleAsync(
            IntegrationEventEnvelope<ConsultationDocumentUploadedV1> envelope,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
