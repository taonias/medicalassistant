namespace MedicalAssistant.EventBus;

public sealed class NonRetryableIntegrationEventException : Exception
{
    public NonRetryableIntegrationEventException(string code)
        : base("Integration event cannot be retried safely.")
    {
        Code = code;
    }

    public string Code { get; }
}
