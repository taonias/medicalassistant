namespace MedicalAssistant.EventBus;

public class DuplicateIntegrationEventSubscriptionException : InvalidOperationException
{
    public DuplicateIntegrationEventSubscriptionException(string message) : base(message)
    {
    }
}

public class IntegrationEventDispatchException : InvalidOperationException
{
    public IntegrationEventDispatchException(string message) : base(message)
    {
    }
}
