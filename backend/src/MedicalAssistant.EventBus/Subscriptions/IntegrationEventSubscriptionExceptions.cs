namespace MedicalAssistant.EventBus;

public class DuplicateIntegrationEventSubscriptionException : InvalidOperationException
{
    public DuplicateIntegrationEventSubscriptionException(string message) : base(message)
    {
    }
}
