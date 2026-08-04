namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqConsumerOptions
{
    public const string SectionName = "RabbitMQ:Consumer";

    public string QueueName { get; set; } = string.Empty;
    public ushort PrefetchCount { get; set; } = 1;
    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
