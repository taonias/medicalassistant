namespace MedicalAssistant.EventBusRabbitMQ;

public class RabbitMqPublishException : InvalidOperationException
{
    public RabbitMqPublishException(string message) : base(message)
    {
    }
}
