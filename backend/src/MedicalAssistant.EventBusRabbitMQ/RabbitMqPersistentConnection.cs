using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqPersistentConnection : IRabbitMqPersistentConnection
{
    private readonly RabbitMqConnectionOptions _options;
    private readonly ILogger<RabbitMqPersistentConnection> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqPersistentConnection(
        IOptions<RabbitMqConnectionOptions> options,
        ILogger<RabbitMqPersistentConnection> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConnected => _connection is { IsOpen: true };

    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        return await connection.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            var delay = _options.InitialReconnectDelay;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var factory = new ConnectionFactory
                    {
                        HostName = _options.Host,
                        Port = _options.Port,
                        VirtualHost = _options.VirtualHost,
                        UserName = _options.Username,
                        Password = _options.Password,
                        AutomaticRecoveryEnabled = true,
                        TopologyRecoveryEnabled = true,
                        RequestedHeartbeat = TimeSpan.FromSeconds(30)
                    };

                    _connection = await factory.CreateConnectionAsync(
                        _options.ClientProvidedName,
                        cancellationToken);
                    return _connection;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(
                        "RabbitMQ connection failed; retrying after {DelaySeconds} seconds.",
                        delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                    delay = TimeSpan.FromTicks(Math.Min(
                        delay.Ticks * 2,
                        _options.MaxReconnectDelay.Ticks));
                }
            }

            throw new OperationCanceledException(cancellationToken);
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
    }
}
