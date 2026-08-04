using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class RabbitMqDeliveryHandlerTests
{
    [Fact]
    public async Task Valid_delivery_dispatches_to_the_typed_handler_and_returns_success()
    {
        var handled = new HandledMessages();
        var services = new ServiceCollection()
            .AddSingleton(handled)
            .AddScoped<AudioUploadedHandler>()
            .BuildServiceProvider();
        var subscriptions = IntegrationEventSubscriptionRegistry.Create(
            ConsultationIntegrationEvents.Registry,
            builder => builder.Subscribe<ConsultationAudioUploadedV1, AudioUploadedHandler>());
        var dispatcher = new IntegrationEventDispatcher(
            services.GetRequiredService<IServiceScopeFactory>(),
            subscriptions);
        var handler = new RabbitMqIntegrationEventDeliveryHandler(dispatcher);
        var envelope = IntegrationEventEnvelope.Create(
            new ConsultationAudioUploadedV1(1, "file-1", "audio/wav", "private://blob/1", 30),
            ConsultationIntegrationEvents.Registry.Resolve<ConsultationAudioUploadedV1>(),
            "medicalassistant.backend",
            "corr-1",
            null);

        var outcome = await handler.HandleAsync(
            new RabbitMqDelivery(
                ConsultationIntegrationEvents.AudioUploadedV1,
                1,
                IntegrationEventSerializer.Serialize(envelope)),
            CancellationToken.None);

        Assert.Equal(RabbitMqDeliveryOutcome.Acknowledge, outcome);
        Assert.Equal(1, handled.ConsultationIds.Single());
    }

    [Fact]
    public async Task Unsupported_contract_returns_dead_letter_outcome()
    {
        var dispatcher = new IntegrationEventDispatcher(
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            IntegrationEventSubscriptionRegistry.Create(
                ConsultationIntegrationEvents.Registry,
                builder => builder.Subscribe<ConsultationAudioUploadedV1, AudioUploadedHandler>()));
        var handler = new RabbitMqIntegrationEventDeliveryHandler(dispatcher);

        var outcome = await handler.HandleAsync(
            new RabbitMqDelivery("consultation.audio-uploaded.v1", 2, "{}"),
            CancellationToken.None);

        Assert.Equal(RabbitMqDeliveryOutcome.DeadLetter, outcome);
    }

    [Fact]
    public async Task Handler_failure_returns_retry_outcome()
    {
        var services = new ServiceCollection()
            .AddScoped<ThrowingAudioUploadedHandler>()
            .BuildServiceProvider();
        var subscriptions = IntegrationEventSubscriptionRegistry.Create(
            ConsultationIntegrationEvents.Registry,
            builder => builder.Subscribe<ConsultationAudioUploadedV1, ThrowingAudioUploadedHandler>());
        var dispatcher = new IntegrationEventDispatcher(
            services.GetRequiredService<IServiceScopeFactory>(),
            subscriptions);
        var handler = new RabbitMqIntegrationEventDeliveryHandler(dispatcher);
        var envelope = IntegrationEventEnvelope.Create(
            new ConsultationAudioUploadedV1(1, "file-1", "audio/wav", "private://blob/1", 30),
            ConsultationIntegrationEvents.Registry.Resolve<ConsultationAudioUploadedV1>(),
            "medicalassistant.backend",
            null,
            null);

        var outcome = await handler.HandleAsync(
            new RabbitMqDelivery(
                ConsultationIntegrationEvents.AudioUploadedV1,
                1,
                IntegrationEventSerializer.Serialize(envelope)),
            CancellationToken.None);

        Assert.Equal(RabbitMqDeliveryOutcome.Retry, outcome);
    }

    private sealed class HandledMessages
    {
        public List<int> ConsultationIds { get; } = [];
    }

    private sealed class AudioUploadedHandler(HandledMessages handledMessages) :
        IIntegrationEventHandler<ConsultationAudioUploadedV1>
    {
        public Task HandleAsync(
            IntegrationEventEnvelope<ConsultationAudioUploadedV1> envelope,
            CancellationToken cancellationToken)
        {
            handledMessages.ConsultationIds.Add(envelope.Payload.ConsultationId);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingAudioUploadedHandler : IIntegrationEventHandler<ConsultationAudioUploadedV1>
    {
        public Task HandleAsync(
            IntegrationEventEnvelope<ConsultationAudioUploadedV1> envelope,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }
}
