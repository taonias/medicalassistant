namespace MedicalAssistant.EventBus;

public class DuplicateIntegrationEventContractException : InvalidOperationException
{
    public DuplicateIntegrationEventContractException(string message) : base(message)
    {
    }
}

public class UnsupportedIntegrationEventContractException : InvalidOperationException
{
    public UnsupportedIntegrationEventContractException(string message) : base(message)
    {
    }
}
