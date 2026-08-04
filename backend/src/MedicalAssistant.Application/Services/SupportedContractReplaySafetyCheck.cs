using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Models.Messaging;
using MedicalAssistant.EventBus;

namespace MedicalAssistant.Application.Services;

public sealed class SupportedContractReplaySafetyCheck : IIntegrationEventReplaySafetyCheck
{
    private readonly IntegrationEventContractRegistry _registry;

    public SupportedContractReplaySafetyCheck(IntegrationEventContractRegistry registry)
    {
        _registry = registry;
    }

    public Task<IntegrationEventReplaySafetyDecision> CheckAsync(
        SafeDeadLetteredIntegrationEventMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _registry.Resolve(metadata.EventType, metadata.EventVersion);
            return Task.FromResult(IntegrationEventReplaySafetyDecision.Safe());
        }
        catch (UnsupportedIntegrationEventContractException)
        {
            return Task.FromResult(IntegrationEventReplaySafetyDecision.Unsafe("unsupported-event-contract"));
        }
    }
}
