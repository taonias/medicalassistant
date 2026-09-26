using System.Text.Json;
using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Models.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace MedicalAssistant.Infrastructure.Messaging;

public sealed class RabbitMqAiRequestPublisher : IAiRequestPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqAiRequestPublisher> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqAiRequestPublisher(
        IOptions<RabbitMqSettings> settings,
        ILogger<RabbitMqAiRequestPublisher> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public Task PublishChatAsync(ChatRequestedMessage message, CancellationToken cancellationToken = default)
        => PublishRequiredAsync(message.EventType, message.CorrelationId, message, cancellationToken);

    public Task PublishIndexAsync(IndexDocumentsRequestedMessage message, CancellationToken cancellationToken = default)
        => PublishBestEffortAsync(message.EventType, message.CorrelationId, message, cancellationToken);

    private async Task PublishRequiredAsync<T>(
        string eventType,
        string correlationId,
        T payload,
        CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
            throw new InvalidOperationException("RabbitMQ publishing is disabled.");

        await PublishAsync(eventType, correlationId, payload, cancellationToken);
    }

    private async Task PublishBestEffortAsync<T>(
        string eventType,
        string correlationId,
        T payload,
        CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("RabbitMQ publishing disabled; skipped {EventType}.", eventType);
            return;
        }

        await PublishAsync(eventType, correlationId, payload, cancellationToken);
    }

    private async Task PublishAsync<T>(
        string eventType,
        string correlationId,
        T payload,
        CancellationToken cancellationToken)
    {
        await _queueLock.WaitAsync(cancellationToken);
        try
        {
            var connection = await GetConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: _settings.AiProcessingQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            var body = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                MessageId = Guid.NewGuid().ToString("N"),
                CorrelationId = correlationId,
                Type = eventType,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _settings.AiProcessingQueue,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Queued {EventType} on {Queue} (correlation {CorrelationId}).",
                eventType,
                _settings.AiProcessingQueue,
                correlationId);
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

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            var factory = new ConnectionFactory
            {
                HostName = _settings.Host,
                Port = _settings.Port,
                VirtualHost = _settings.VirtualHost,
                UserName = _settings.Username,
                Password = _settings.Password,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(30),
            };

            _connection = await factory.CreateConnectionAsync(
                $"medical-assistant-api-ai-request-{Environment.MachineName}",
                cancellationToken);

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
            await _connection.DisposeAsync();
            _connection = null;
        }

        _connectionLock.Dispose();
        _queueLock.Dispose();
    }
}
