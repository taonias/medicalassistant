namespace MedicalAssistant.EventBus;

public sealed record IntegrationEventContractDescriptor(
    Type ClrType,
    string EventType,
    int EventVersion);
