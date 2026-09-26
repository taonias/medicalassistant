using System.Text.Json;
using MedicalAssistant.AiModule.Models;
using MedicalAssistant.AiModule.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace MedicalAssistant.AiModule.Services;

public sealed class RabbitMqAiResultPublisher : IAiResultPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqAiResultPublisher> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqAiResultPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqAiResultPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(AiResultMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Connection))
            throw new InvalidOperationException("RabbitMq:Connection (RabbitMqConnection) is not configured.");

        if (string.IsNullOrWhiteSpace(_options.AiResultsQueue))
            throw new InvalidOperationException("RabbitMq:AiResultsQueue is not configured.");

        await _queueLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await channel.QueueDeclareAsync(
                queue: _options.AiResultsQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                MessageId = Guid.NewGuid().ToString("N"),
                CorrelationId = message.CorrelationId,
                Type = message.EventType,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _options.AiResultsQueue,
                mandatory: false,
                basicProperties: properties,
                body: payload,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Queued dummy {EventType} on {Queue} (correlation {CorrelationId}).",
                message.EventType,
                _options.AiResultsQueue,
                message.CorrelationId);
        }
        finally
        {
            _queueLock.Release();
        }
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            if (_connection is not null)
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
                _connection = null;
            }

            var factory = new ConnectionFactory
            {
                Uri = new Uri(_options.Connection!),
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(30),
            };

            _connection = await factory.CreateConnectionAsync(
                $"medical-assistant-aimodule-{Environment.MachineName}",
                cancellationToken).ConfigureAwait(false);

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        _connectionLock.Dispose();
        _queueLock.Dispose();
    }
}
