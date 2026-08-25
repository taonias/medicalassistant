using RabbitMQ.Client;

namespace MedicalAssistant.EventBusRabbitMQ;

public sealed record RabbitMqPublishRequest(
    string ExchangeName,
    string RoutingKey,
    bool Mandatory,
    BasicProperties Properties,
    ReadOnlyMemory<byte> Body);
