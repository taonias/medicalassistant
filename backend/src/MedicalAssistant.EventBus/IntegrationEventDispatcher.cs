using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.EventBus;

public sealed class IntegrationEventDispatcher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IntegrationEventSubscriptionRegistry _subscriptions;

    public IntegrationEventDispatcher(
        IServiceScopeFactory scopeFactory,
        IntegrationEventSubscriptionRegistry subscriptions)
    {
        _scopeFactory = scopeFactory;
        _subscriptions = subscriptions;
    }

    public async Task DispatchAsync(
        string eventType,
        int eventVersion,
        string envelopeJson,
        CancellationToken cancellationToken)
    {
        var subscription = _subscriptions.Resolve(eventType, eventVersion);
        var envelope = IntegrationEventSerializer.Deserialize(envelopeJson, subscription.Contract);
        ValidateEnvelopeMetadata(envelope, subscription.Contract);

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetService(subscription.HandlerType);
        if (handler is null)
        {
            throw new IntegrationEventDispatchException(
                $"Handler '{subscription.HandlerType.FullName}' is not registered in the service provider.");
        }

        var handlerInterface = typeof(IIntegrationEventHandler<>).MakeGenericType(subscription.Contract.ClrType);
        var handleMethod = handlerInterface.GetMethod(nameof(IIntegrationEventHandler<object>.HandleAsync))
            ?? throw new IntegrationEventDispatchException(
                $"Handler '{subscription.HandlerType.FullName}' does not expose a HandleAsync method.");
        var result = handleMethod.Invoke(handler, [envelope, cancellationToken]);

        if (result is not Task task)
        {
            throw new IntegrationEventDispatchException(
                $"Handler '{subscription.HandlerType.FullName}' returned an invalid result.");
        }

        await task.ConfigureAwait(false);
    }

    private static void ValidateEnvelopeMetadata(
        object envelope,
        IntegrationEventContractDescriptor contract)
    {
        var eventType = envelope.GetType().GetProperty(nameof(IntegrationEventEnvelope<object>.EventType))?
            .GetValue(envelope) as string;
        var eventVersion = envelope.GetType().GetProperty(nameof(IntegrationEventEnvelope<object>.EventVersion))?
            .GetValue(envelope) as int?;

        if (eventType != contract.EventType || eventVersion != contract.EventVersion)
        {
            throw new IntegrationEventDispatchException(
                $"Envelope metadata does not match subscription '{contract.EventType}' version {contract.EventVersion}.");
        }
    }
}
