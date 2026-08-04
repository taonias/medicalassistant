namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqTopologyOptions
{
    public const string SectionName = "RabbitMQ:Topology";

    public string ExchangeName { get; set; } = "medicalassistant.events";
    public string SubscriberName { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
    public TimeSpan[] RetryDelays { get; set; } =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1)
    ];
}
