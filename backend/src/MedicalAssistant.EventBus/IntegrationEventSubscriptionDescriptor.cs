namespace MedicalAssistant.EventBus;

public sealed record IntegrationEventSubscriptionDescriptor(
    IntegrationEventContractDescriptor Contract,
    Type HandlerType);
