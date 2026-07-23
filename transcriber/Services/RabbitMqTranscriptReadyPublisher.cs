using System.Text.Json;
using MedicalAssistant.Transcriber.Models;
using MedicalAssistant.Transcriber.Options;
using MedicalAssistant.Transcriber.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace MedicalAssistant.Transcriber.Services;

public sealed class RabbitMqTranscriptReadyPublisher : ITranscriptReadyPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqTranscriptReadyPublisher> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqTranscriptReadyPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqTranscriptReadyPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(
        TranscriptReadyMessage message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Connection))
            throw new InvalidOperationException("RabbitMq:Connection (RabbitMqConnection) is not configured.");

        if (string.IsNullOrWhiteSpace(_options.ConsultationTranscriptQueueName))
            throw new InvalidOperationException("RabbitMq:ConsultationTranscriptQueueName is not configured.");

        await _queueLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await channel.QueueDeclareAsync(
                queue: _options.ConsultationTranscriptQueueName,
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
                routingKey: _options.ConsultationTranscriptQueueName,
                mandatory: false,
                basicProperties: properties,
                body: payload,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Queued {EventType} on {Queue} for transcript {TranscriptId} (consultation {ConsultationId}).",
                message.EventType,
                _options.ConsultationTranscriptQueueName,
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
                $"medical-assistant-transcriber-{Environment.MachineName}",
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
