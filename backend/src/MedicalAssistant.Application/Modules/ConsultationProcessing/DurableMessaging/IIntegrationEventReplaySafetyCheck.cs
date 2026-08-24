using MedicalAssistant.Application.Models.Messaging;

namespace MedicalAssistant.Application.Contracts.Messaging;

public interface IIntegrationEventReplaySafetyCheck
{
    Task<IntegrationEventReplaySafetyDecision> CheckAsync(
        SafeDeadLetteredIntegrationEventMetadata metadata,
        CancellationToken cancellationToken = default);
}
