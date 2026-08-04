namespace MedicalAssistant.EventBus;

public sealed class IntegrationEventSubscriptionRegistry
{
    private readonly IReadOnlyDictionary<WireContractKey, IntegrationEventSubscriptionDescriptor> _subscriptions;

    private IntegrationEventSubscriptionRegistry(
        IReadOnlyDictionary<WireContractKey, IntegrationEventSubscriptionDescriptor> subscriptions)
    {
        _subscriptions = subscriptions;
    }

    public IReadOnlyCollection<IntegrationEventSubscriptionDescriptor> Subscriptions =>
        _subscriptions.Values.ToArray();

    public static IntegrationEventSubscriptionRegistry Create(
        IntegrationEventContractRegistry contracts,
        Action<IntegrationEventSubscriptionRegistryBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(contracts);
        var builder = new IntegrationEventSubscriptionRegistryBuilder(contracts);
        configure(builder);
        return builder.Build();
    }

    public IntegrationEventSubscriptionDescriptor Resolve(string eventType, int eventVersion)
    {
        var key = new WireContractKey(eventType, eventVersion);
        if (_subscriptions.TryGetValue(key, out var descriptor))
        {
            return descriptor;
        }

        throw new UnsupportedIntegrationEventContractException(
            $"No subscription is registered for '{eventType}' version {eventVersion}.");
    }

    private sealed record WireContractKey(string EventType, int EventVersion);

    public sealed class IntegrationEventSubscriptionRegistryBuilder
    {
        private readonly IntegrationEventContractRegistry _contracts;
        private readonly Dictionary<WireContractKey, IntegrationEventSubscriptionDescriptor> _subscriptions = [];

        internal IntegrationEventSubscriptionRegistryBuilder(IntegrationEventContractRegistry contracts)
        {
            _contracts = contracts;
        }

        public IntegrationEventSubscriptionRegistryBuilder Subscribe<TPayload, THandler>()
            where THandler : IIntegrationEventHandler<TPayload>
        {
            var contract = _contracts.Resolve<TPayload>();
            var key = new WireContractKey(contract.EventType, contract.EventVersion);

            if (_subscriptions.ContainsKey(key))
            {
                throw new DuplicateIntegrationEventSubscriptionException(
                    $"A handler is already registered for '{contract.EventType}' version {contract.EventVersion}.");
            }

            _subscriptions.Add(
                key,
                new IntegrationEventSubscriptionDescriptor(contract, typeof(THandler)));
            return this;
        }

        internal IntegrationEventSubscriptionRegistry Build() => new(_subscriptions);
    }
}
