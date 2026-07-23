using System.Text.Json;
using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Models.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace MedicalAssistant.Infrastructure.Messaging;

public sealed class RabbitMqConsultationProcessingPublisher : IConsultationProcessingPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqConsultationProcessingPublisher> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConsultationProcessingPublisher(
        IOptions<RabbitMqSettings> settings,
        ILogger<RabbitMqConsultationProcessingPublisher> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task PublishAsync(
        ConsultationProcessingMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation(
                "RabbitMQ publishing disabled; skipped {EventType} for consultation {ConsultationId}.",
                message.EventType,
                message.ConsultationId);
            return;
        }

        await _queueLock.WaitAsync(cancellationToken);
        try
        {
            var connection = await GetConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await EnsureQueueAsync(channel, cancellationToken);
            await PublishOnChannelAsync(channel, message, cancellationToken);

            _logger.LogInformation(
                "Queued {EventType} on {Queue} for consultation {ConsultationId}.",
                message.EventType,
                _settings.ConsultationProcessingQueue,
                message.ConsultationId);
        }
        finally
        {
            _queueLock.Release();
        }
    }

    public async Task RemovePendingForConsultationAsync(
        int consultationId,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
            return;

        await _queueLock.WaitAsync(cancellationToken);
        try
        {
            var connection = await GetConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await EnsureQueueAsync(channel, cancellationToken);

            var keepers = new List<ConsultationProcessingMessage>();
            var removed = 0;

            while (true)
            {
                var result = await channel.BasicGetAsync(
                    _settings.ConsultationProcessingQueue,
                    autoAck: false,
                    cancellationToken: cancellationToken);

                if (result is null)
                    break;

                ConsultationProcessingMessage? message = null;
                try
                {
                    message = JsonSerializer.Deserialize<ConsultationProcessingMessage>(
                        result.Body.ToArray(),
                        JsonOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(
                        "Dropping malformed queue message while removing consultation {ConsultationId}: {Error}",
                        consultationId,
                        ex.Message);
                }

                if (message is not null && message.ConsultationId == consultationId)
                {
                    removed++;
                }
                else if (message is not null)
                {
                    keepers.Add(message);
                }

                await channel.BasicAckAsync(result.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
            }

            foreach (var keeper in keepers)
            {
                await PublishOnChannelAsync(channel, keeper, cancellationToken);
            }

            _logger.LogInformation(
                "Removed {RemovedCount} pending message(s) for consultation {ConsultationId}; restored {KeptCount}.",
                removed,
                consultationId,
                keepers.Count);
        }
        finally
        {
            _queueLock.Release();
        }
    }

    private async Task EnsureQueueAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.QueueDeclareAsync(
            queue: _settings.ConsultationProcessingQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    private async Task PublishOnChannelAsync(
        IChannel channel,
        ConsultationProcessingMessage message,
        CancellationToken cancellationToken)
    {
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
            routingKey: _settings.ConsultationProcessingQueue,
            mandatory: false,
            basicProperties: properties,
            body: payload,
            cancellationToken: cancellationToken);
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
                $"medical-assistant-api-{Environment.MachineName}",
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
