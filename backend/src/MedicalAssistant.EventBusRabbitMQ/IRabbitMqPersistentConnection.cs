using RabbitMQ.Client;

namespace MedicalAssistant.EventBusRabbitMQ;

public interface IRabbitMqPersistentConnection : IAsyncDisposable
{
    bool IsConnected { get; }

    Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken);

    Task<IChannel> CreateChannelAsync(
        CreateChannelOptions options,
        CancellationToken cancellationToken);
}
