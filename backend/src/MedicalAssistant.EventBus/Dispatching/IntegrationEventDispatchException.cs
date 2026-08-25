namespace MedicalAssistant.EventBus;

public class IntegrationEventDispatchException : InvalidOperationException
{
    public IntegrationEventDispatchException(string message) : base(message)
    {
    }
}
