namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqTopologyOptions
{
    public const string SectionName = "RabbitMQ:Topology";

    public string ExchangeName { get; set; } = "medicalassistant.events";
    public string SubscriberName { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;

    // Automatic retries are intentionally disabled: each subscriber runs a single live
    // queue plus a dead-letter queue (no delayed-retry queues). Business failures are
    // recorded to the database as Failed with a reason and surfaced to the doctor, who
    // triggers a manual retry from the UI. With no retry delays configured, the consumer's
    // Retry outcome falls straight through to the dead-letter queue (see
    // RabbitMqSubscriberTopologyPlan and RabbitMqHostedConsumer), which we keep as a safety
    // net for poison or otherwise unrecordable messages. To re-enable automatic delayed
    // retries, populate this array with one delay per attempt.
    public TimeSpan[] RetryDelays { get; set; } = [];
}
