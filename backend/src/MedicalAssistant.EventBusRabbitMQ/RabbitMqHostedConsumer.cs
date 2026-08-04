using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqHostedConsumer : BackgroundService
{
    private readonly IRabbitMqPersistentConnection _connection;
    private readonly IRabbitMqDeliveryHandler _deliveryHandler;
    private readonly RabbitMqConsumerOptions _options;
    private readonly ILogger<RabbitMqHostedConsumer> _logger;
    private IChannel? _channel;

    public RabbitMqHostedConsumer(
        IRabbitMqPersistentConnection connection,
        IRabbitMqDeliveryHandler deliveryHandler,
        IOptions<RabbitMqConsumerOptions> options,
        ILogger<RabbitMqHostedConsumer> logger)
    {
        _connection = connection;
        _deliveryHandler = deliveryHandler;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.QueueName);

        _channel = await _connection.CreateChannelAsync(stoppingToken);
        await _channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: _options.PrefetchCount,
            global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += HandleDeliveryAsync;

        await _channel.BasicConsumeAsync(
            queue: _options.QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleDeliveryAsync(object sender, BasicDeliverEventArgs args)
    {
        if (_channel is null)
        {
            return;
        }

        var eventType = args.RoutingKey;
        var eventVersion = RabbitMqRoutingKeyVersion.Parse(eventType);
        var envelopeJson = Encoding.UTF8.GetString(args.Body.Span);
        var outcome = await _deliveryHandler.HandleAsync(
            new RabbitMqDelivery(eventType, eventVersion, envelopeJson),
            CancellationToken.None);

        switch (outcome)
        {
            case RabbitMqDeliveryOutcome.Acknowledge:
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                break;
            case RabbitMqDeliveryOutcome.Retry:
                await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true);
                break;
            case RabbitMqDeliveryOutcome.DeadLetter:
                await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
                break;
            default:
                throw new InvalidOperationException($"Unknown RabbitMQ delivery outcome '{outcome}'.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        using var drain = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drain.CancelAfter(_options.ShutdownDrainTimeout);

        try
        {
            await base.StopAsync(drain.Token);
        }
        finally
        {
            if (_channel is not null)
            {
                await _channel.DisposeAsync();
                _channel = null;
            }
        }
    }
}
