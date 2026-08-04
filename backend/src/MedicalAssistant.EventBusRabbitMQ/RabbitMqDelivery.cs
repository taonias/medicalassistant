namespace MedicalAssistant.EventBusRabbitMQ;

public sealed record RabbitMqDelivery(
    string EventType,
    int EventVersion,
    string EnvelopeJson);
