namespace MedicalAssistant.Transcriber.Options;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>AMQP URI (same value as Functions <c>RabbitMqConnection</c>).</summary>
    public string? Connection { get; set; }

    public string ConsultationTranscriptQueueName { get; set; } = "consultation.transcript";
}
