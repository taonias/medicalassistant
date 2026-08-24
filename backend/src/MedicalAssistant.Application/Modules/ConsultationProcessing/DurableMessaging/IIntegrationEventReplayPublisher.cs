using MedicalAssistant.Application.Models.Messaging;

namespace MedicalAssistant.Application.Contracts.Messaging;

public interface IIntegrationEventReplayPublisher
{
    Task PublishAsync(
        IntegrationEventReplayMessage message,
        CancellationToken cancellationToken = default);
}
