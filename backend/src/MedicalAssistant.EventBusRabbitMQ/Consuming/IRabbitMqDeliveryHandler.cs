namespace MedicalAssistant.EventBusRabbitMQ;

public interface IRabbitMqDeliveryHandler
{
    Task<RabbitMqDeliveryOutcome> HandleAsync(
        RabbitMqDelivery delivery,
        CancellationToken cancellationToken);
}
