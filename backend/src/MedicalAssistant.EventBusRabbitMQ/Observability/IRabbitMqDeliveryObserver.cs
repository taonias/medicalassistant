namespace MedicalAssistant.EventBusRabbitMQ;

public interface IRabbitMqDeliveryObserver
{
    void DeliveryHandled(string eventType, RabbitMqDeliveryOutcome outcome);

    void RoutedToRetry(string eventType, int retryAttempt);

    void RoutedToDeadLetter(string eventType);
}
