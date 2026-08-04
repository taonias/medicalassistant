using RabbitMQ.Client;

namespace MedicalAssistant.EventBusRabbitMQ;

public static class RabbitMqConnectionFactoryBuilder
{
    public static ConnectionFactory Create(RabbitMqConnectionOptions options) =>
        new()
        {
            HostName = options.Host,
            Port = options.Port,
            VirtualHost = options.VirtualHost,
            UserName = options.Username,
            Password = options.Password,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            RequestedHeartbeat = TimeSpan.FromSeconds(30),
            Ssl = new SslOption
            {
                Enabled = options.UseTls,
                ServerName = string.IsNullOrWhiteSpace(options.TlsServerName)
                    ? options.Host
                    : options.TlsServerName
            }
        };
}
