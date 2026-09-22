namespace MedicalAssistant.Application.Models;

public class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "p@ssw0rd";
    public string TranscriberProcessingQueue { get; set; } = "transcription.requests";
    public string AiProcessingQueue { get; set; } = "ai.requests";
    public bool Enabled { get; set; } = true;
}
