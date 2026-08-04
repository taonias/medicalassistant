namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqPublishOptions
{
    public const string SectionName = "RabbitMQ:Publisher";

    public string ExchangeName { get; set; } = "medicalassistant.events";
    public TimeSpan ConfirmTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
