using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace MedicalAssistance.Ingestion.Api.Ingestions;

/// <summary>
/// Connection settings for publishing integration events to the shared event bus. When
/// <see cref="Host"/> is empty the publisher and relay stay inert, so the service still runs
/// standalone (HTTP-only) with no broker.
/// </summary>
public sealed class RabbitMqPublishOptions
{
    public const string SectionName = "RabbitMQ";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ExchangeName { get; set; } = "medicalassistant.events";
    public string ClientProvidedName { get; set; } = "clinical-knowledge";

    public bool Enabled => !string.IsNullOrWhiteSpace(Host);
}

/// <summary>
/// Publishes a pre-built integration-event envelope to the shared direct exchange with
/// publisher confirms — a publish is only reported successful once the broker acknowledges it,
/// so the outbox relay never marks a row published on a lost message. Holds one lazily-opened,
/// reused channel; reopened if it drops.
/// </summary>
public sealed class RabbitMqEventPublisher(
    IOptions<RabbitMqPublishOptions> options,
    ILogger<RabbitMqEventPublisher> logger) : IAsyncDisposable
{
    private readonly RabbitMqPublishOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync(
        string routingKey, string messageId, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        var channel = await EnsureChannelAsync(ct);
        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = messageId
        };

        // The channel has publisher-confirm tracking enabled, so this awaits the broker ack.
        await channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        await _gate.WaitAsync(ct);
        try
        {
            if (_channel is { IsOpen: true })
                return _channel;

            if (_connection is not { IsOpen: true })
            {
                var factory = new ConnectionFactory
                {
                    HostName = _options.Host,
                    Port = _options.Port,
                    VirtualHost = _options.VirtualHost,
                    UserName = _options.Username,
                    Password = _options.Password
                };
                _connection = await factory.CreateConnectionAsync(_options.ClientProvidedName, ct);
            }

            _channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true),
                ct);

            // Idempotent: the exchange is declared identically by every service that uses it.
            await _channel.ExchangeDeclareAsync(
                _options.ExchangeName, ExchangeType.Direct, durable: true, autoDelete: false,
                cancellationToken: ct);

            logger.LogInformation(
                "Opened RabbitMQ publisher channel to exchange {Exchange}", _options.ExchangeName);
            return _channel;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();
        if (_connection is not null)
            await _connection.DisposeAsync();
        _gate.Dispose();
    }
}
