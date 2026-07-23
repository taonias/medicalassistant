using System.Text.Json;
using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Models.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace MedicalAssistant.Infrastructure.Messaging;

public sealed class RabbitMqTranscriptReadyPublisher : ITranscriptReadyPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqTranscriptReadyPublisher> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqTranscriptReadyPublisher(
        IOptions<RabbitMqSettings> settings,
        ILogger<RabbitMqTranscriptReadyPublisher> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task PublishAsync(
        TranscriptReadyMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation(
                "RabbitMQ publishing disabled; skipped {EventType} for transcript {TranscriptId}.",
                message.EventType,
                message.TranscriptId);
            return;
        }

        await _queueLock.WaitAsync(cancellationToken);
        try
        {
            var connection = await GetConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                queue: _settings.ConsultationTranscriptQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

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
                routingKey: _settings.ConsultationTranscriptQueue,
                mandatory: false,
                basicProperties: properties,
                body: payload,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Queued {EventType} on {Queue} for transcript {TranscriptId} (consultation {ConsultationId}).",
                message.EventType,
                _settings.ConsultationTranscriptQueue,
                message.TranscriptId,
                message.ConsultationId);
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
                $"medical-assistant-api-transcript-{Environment.MachineName}",
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
