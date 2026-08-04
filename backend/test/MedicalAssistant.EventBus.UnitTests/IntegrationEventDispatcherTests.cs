using MedicalAssistant.EventBus.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.EventBus.UnitTests;

public class IntegrationEventDispatcherTests
{
    [Fact]
    public void Subscription_registry_rejects_duplicate_or_unsupported_bindings()
    {
        Assert.Throws<DuplicateIntegrationEventSubscriptionException>(() =>
            IntegrationEventSubscriptionRegistry.Create(
                ConsultationIntegrationEvents.Registry,
                builder => builder
                    .Subscribe<ConsultationAudioUploadedV1, RecordingHandler>()
                    .Subscribe<ConsultationAudioUploadedV1, AnotherRecordingHandler>()));

        Assert.Throws<UnsupportedIntegrationEventContractException>(() =>
            IntegrationEventSubscriptionRegistry.Create(
                ConsultationIntegrationEvents.Registry,
                builder => builder.Subscribe<UnregisteredPayload, UnregisteredPayloadHandler>()));
    }

    [Fact]
    public async Task Dispatcher_deserializes_the_envelope_and_invokes_the_typed_handler_in_a_scope()
    {
        var services = new ServiceCollection();
        services.AddScoped<RecordingHandler>();
        services.AddScoped<IIntegrationEventHandler<ConsultationAudioUploadedV1>>(provider =>
            provider.GetRequiredService<RecordingHandler>());
        services.AddSingleton<HandledMessages>();
        var provider = services.BuildServiceProvider();

        var subscriptions = IntegrationEventSubscriptionRegistry.Create(
            ConsultationIntegrationEvents.Registry,
            builder => builder.Subscribe<ConsultationAudioUploadedV1, RecordingHandler>());
        var dispatcher = new IntegrationEventDispatcher(provider.GetRequiredService<IServiceScopeFactory>(), subscriptions);
        var payload = new ConsultationAudioUploadedV1(
            ConsultationId: 12,
            FileId: "file-12",
            ContentType: "audio/wav",
            StorageObjectReference: "private://recordings/12",
            DurationSeconds: 42);
        var envelope = IntegrationEventEnvelope.Create(
            payload,
            ConsultationIntegrationEvents.Registry.Resolve<ConsultationAudioUploadedV1>(),
            producer: "medicalassistant.backend",
            correlationId: "corr-12",
            causationId: null);
        var serialized = IntegrationEventSerializer.Serialize(envelope);

        await dispatcher.DispatchAsync(
            ConsultationIntegrationEvents.AudioUploadedV1,
            1,
            serialized,
            CancellationToken.None);

        var handled = provider.GetRequiredService<HandledMessages>();
        Assert.Single(handled.AudioUploads);
        Assert.Equal(12, handled.AudioUploads[0].ConsultationId);
    }

    [Fact]
    public async Task Dispatcher_rejects_a_binding_when_no_handler_is_registered()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var subscriptions = IntegrationEventSubscriptionRegistry.Create(
            ConsultationIntegrationEvents.Registry,
            builder => builder.Subscribe<ConsultationAudioUploadedV1, RecordingHandler>());
        var dispatcher = new IntegrationEventDispatcher(provider.GetRequiredService<IServiceScopeFactory>(), subscriptions);
        var payload = new ConsultationAudioUploadedV1(12, "file-12", "audio/wav", "private://recordings/12", 42);
        var envelope = IntegrationEventEnvelope.Create(
            payload,
            ConsultationIntegrationEvents.Registry.Resolve<ConsultationAudioUploadedV1>(),
            "medicalassistant.backend",
            null,
            null);

        await Assert.ThrowsAsync<IntegrationEventDispatchException>(() =>
            dispatcher.DispatchAsync(
                ConsultationIntegrationEvents.AudioUploadedV1,
                1,
                IntegrationEventSerializer.Serialize(envelope),
                CancellationToken.None));
    }

    private sealed record UnregisteredPayload;

    private sealed class HandledMessages
    {
        public List<ConsultationAudioUploadedV1> AudioUploads { get; } = [];
    }

    private sealed class RecordingHandler(HandledMessages handledMessages) :
        IIntegrationEventHandler<ConsultationAudioUploadedV1>
    {
        public Task HandleAsync(
            IntegrationEventEnvelope<ConsultationAudioUploadedV1> envelope,
            CancellationToken cancellationToken)
        {
            handledMessages.AudioUploads.Add(envelope.Payload);
            return Task.CompletedTask;
        }
    }

    private sealed class AnotherRecordingHandler : IIntegrationEventHandler<ConsultationAudioUploadedV1>
    {
        public Task HandleAsync(
            IntegrationEventEnvelope<ConsultationAudioUploadedV1> envelope,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class UnregisteredPayloadHandler : IIntegrationEventHandler<UnregisteredPayload>
    {
        public Task HandleAsync(
            IntegrationEventEnvelope<UnregisteredPayload> envelope,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
