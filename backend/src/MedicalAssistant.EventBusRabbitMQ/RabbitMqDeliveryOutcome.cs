namespace MedicalAssistant.EventBusRabbitMQ;

public enum RabbitMqDeliveryOutcome
{
    Acknowledge = 0,
    Retry = 1,
    DeadLetter = 2
}
