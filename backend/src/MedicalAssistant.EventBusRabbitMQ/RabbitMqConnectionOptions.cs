namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqConnectionOptions
{
    public const string SectionName = "RabbitMQ";

    public string Host { get; set; } = string.Empty;
    public string HostName
    {
        get => Host;
        set => Host = value;
    }

    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = string.Empty;
    public string UserName
    {
        get => Username;
        set => Username = value;
    }

    public string Password { get; set; } = string.Empty;
    public string ClientProvidedName { get; set; } = "medicalassistant-eventbus";
    public bool UseTls { get; set; }
    public string? TlsServerName { get; set; }
    public TimeSpan InitialReconnectDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxReconnectDelay { get; set; } = TimeSpan.FromSeconds(30);
}
