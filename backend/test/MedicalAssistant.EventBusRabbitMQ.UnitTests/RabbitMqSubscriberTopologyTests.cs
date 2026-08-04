using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;

namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class RabbitMqSubscriberTopologyTests
{
    [Fact]
    public void Topology_plan_uses_the_shared_direct_exchange_and_subscriber_owned_queues()
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
        Assert.Equal(5, plan.RetryQueues.Count);
        Assert.All(plan.RetryQueues, queue => Assert.StartsWith("medicalassistant.transcription.q.retry.", queue.Name));
        Assert.Single(plan.Bindings);
        Assert.Equal(ConsultationIntegrationEvents.AudioUploadedV1, plan.Bindings[0].RoutingKey);
        Assert.Equal("medicalassistant.transcription.q", plan.Bindings[0].QueueName);
    }

    [Fact]
    public void Retry_queues_dead_letter_back_to_the_exchange_with_the_original_routing_key()
    {
        var subscriptions = IntegrationEventSubscriptionRegistry.Create(
            ConsultationIntegrationEvents.Registry,
            builder => builder.Subscribe<ConsultationAudioUploadedV1, AudioUploadedHandler>());
        var plan = RabbitMqSubscriberTopologyPlan.Create(
            new RabbitMqTopologyOptions
            {
                QueueName = "medicalassistant.transcription.q",
                SubscriberName = "transcription-worker"
            },
            subscriptions);

        Assert.All(plan.RetryQueues, queue =>
        {
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
