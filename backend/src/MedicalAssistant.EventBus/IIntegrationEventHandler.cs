namespace MedicalAssistant.EventBus;

public interface IIntegrationEventHandler<TPayload>
{
    Task HandleAsync(
        IntegrationEventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken);
}
