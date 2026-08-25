namespace MedicalAssistant.EventBus;

public sealed class IntegrationEventContractRegistry
{
    private readonly IReadOnlyDictionary<Type, IntegrationEventContractDescriptor> _byClrType;
    private readonly IReadOnlyDictionary<WireContractKey, IntegrationEventContractDescriptor> _byWireContract;

    private IntegrationEventContractRegistry(
        IReadOnlyDictionary<Type, IntegrationEventContractDescriptor> byClrType,
        IReadOnlyDictionary<WireContractKey, IntegrationEventContractDescriptor> byWireContract)
    {
        _byClrType = byClrType;
        _byWireContract = byWireContract;
    }

    public static IntegrationEventContractRegistry Create(Action<IntegrationEventContractRegistryBuilder> configure)
    {
        var builder = new IntegrationEventContractRegistryBuilder();
        configure(builder);
        return builder.Build();
    }

    public IntegrationEventContractDescriptor Resolve<TPayload>() => Resolve(typeof(TPayload));

    public IntegrationEventContractDescriptor Resolve(Type clrType)
    {
        if (_byClrType.TryGetValue(clrType, out var descriptor))
        {
            return descriptor;
        }

        throw new UnsupportedIntegrationEventContractException(
            $"Integration event contract '{clrType.FullName}' is not registered.");
    }

    public IntegrationEventContractDescriptor Resolve(string eventType, int eventVersion)
    {
        var key = new WireContractKey(eventType, eventVersion);
        if (_byWireContract.TryGetValue(key, out var descriptor))
        {
            return descriptor;
        }

        throw new UnsupportedIntegrationEventContractException(
            $"Integration event contract '{eventType}' version {eventVersion} is not supported.");
    }

    private sealed record WireContractKey(string EventType, int EventVersion);

    public sealed class IntegrationEventContractRegistryBuilder
    {
        private readonly Dictionary<Type, IntegrationEventContractDescriptor> _byClrType = [];
        private readonly Dictionary<WireContractKey, IntegrationEventContractDescriptor> _byWireContract = [];

        public IntegrationEventContractRegistryBuilder Add<TPayload>(string eventType, int eventVersion)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(eventVersion);

            var clrType = typeof(TPayload);
            var descriptor = new IntegrationEventContractDescriptor(clrType, eventType, eventVersion);

            if (_byClrType.ContainsKey(clrType))
            {
                throw new DuplicateIntegrationEventContractException(
                    $"Integration event contract '{clrType.FullName}' is already registered.");
            }

            var key = new WireContractKey(eventType, eventVersion);
            if (_byWireContract.ContainsKey(key))
            {
                throw new DuplicateIntegrationEventContractException(
                    $"Integration event contract '{eventType}' version {eventVersion} is already registered.");
            }

            _byClrType.Add(clrType, descriptor);
            _byWireContract.Add(key, descriptor);
            return this;
        }

        internal IntegrationEventContractRegistry Build() =>
            new(_byClrType, _byWireContract);
    }
}
