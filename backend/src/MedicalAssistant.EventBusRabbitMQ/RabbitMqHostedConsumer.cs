using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MedicalAssistant.EventBus;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqHostedConsumer : BackgroundService
{
    private const string RetryAttemptHeader = "x-medicalassistant-retry-attempt";

    private readonly IRabbitMqPersistentConnection _connection;
    private readonly IRabbitMqDeliveryHandler _deliveryHandler;
    private readonly RabbitMqConsumerOptions _options;
    private readonly RabbitMqTopologyOptions _topologyOptions;
    private readonly IntegrationEventSubscriptionRegistry _subscriptions;
    private readonly RabbitMqSubscriberTopologyDeclarer _topologyDeclarer;
    private readonly ILogger<RabbitMqHostedConsumer> _logger;
    private IChannel? _channel;
    private RabbitMqSubscriberTopologyPlan? _topologyPlan;

    public RabbitMqHostedConsumer(
        IRabbitMqPersistentConnection connection,
        IRabbitMqDeliveryHandler deliveryHandler,
        IOptions<RabbitMqConsumerOptions> options,
        IOptions<RabbitMqTopologyOptions> topologyOptions,
        IntegrationEventSubscriptionRegistry subscriptions,
        RabbitMqSubscriberTopologyDeclarer topologyDeclarer,
        ILogger<RabbitMqHostedConsumer> logger)
    {
        _connection = connection;
        _deliveryHandler = deliveryHandler;
        _options = options.Value;
        _topologyOptions = topologyOptions.Value;
        if (string.IsNullOrWhiteSpace(_topologyOptions.QueueName))
        {
            _topologyOptions.QueueName = _options.QueueName;
        }

        _subscriptions = subscriptions;
        _topologyDeclarer = topologyDeclarer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.QueueName);

        _channel = await _connection.CreateChannelAsync(stoppingToken);
        var plan = RabbitMqSubscriberTopologyPlan.Create(_topologyOptions, _subscriptions);
        _topologyPlan = plan;
        await _topologyDeclarer.DeclareAsync(_channel, plan, stoppingToken);

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
                await RouteToRetryOrDeadLetterAsync(args, CancellationToken.None);
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                break;
            case RabbitMqDeliveryOutcome.DeadLetter:
                await RouteToDeadLetterAsync(args, CancellationToken.None);
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                break;
            default:
                throw new InvalidOperationException($"Unknown RabbitMQ delivery outcome '{outcome}'.");
        }
    }

    private async Task RouteToRetryOrDeadLetterAsync(
        BasicDeliverEventArgs args,
        CancellationToken cancellationToken)
    {
        if (_channel is null || _topologyPlan is null)
        {
            return;
        }

        var nextAttempt = GetRetryAttempt(args.BasicProperties.Headers) + 1;
        if (nextAttempt > _topologyPlan.RetryQueues.Count)
        {
            await RouteToDeadLetterAsync(args, cancellationToken);
            return;
        }

        var retryQueue = _topologyPlan.RetryQueues[nextAttempt - 1];
        var properties = CreateForwardedProperties(args, nextAttempt);
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: retryQueue.Name,
            mandatory: true,
            basicProperties: properties,
            body: args.Body,
            cancellationToken: cancellationToken);

        _logger.LogWarning(
            "Routed integration event {EventType} delivery {DeliveryTag} to delayed retry queue {RetryQueue} at attempt {RetryAttempt}.",
            args.RoutingKey,
            args.DeliveryTag,
            retryQueue.Name,
            nextAttempt);
    }

    private async Task RouteToDeadLetterAsync(
        BasicDeliverEventArgs args,
        CancellationToken cancellationToken)
    {
        if (_channel is null || _topologyPlan is null)
        {
            return;
        }

        var properties = CreateForwardedProperties(args, GetRetryAttempt(args.BasicProperties.Headers));
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _topologyPlan.DeadLetterQueue.Name,
            mandatory: true,
            basicProperties: properties,
            body: args.Body,
            cancellationToken: cancellationToken);

        _logger.LogError(
            "Routed integration event {EventType} delivery {DeliveryTag} to dead-letter queue {DeadLetterQueue}.",
            args.RoutingKey,
            args.DeliveryTag,
            _topologyPlan.DeadLetterQueue.Name);
    }

    private static BasicProperties CreateForwardedProperties(
        BasicDeliverEventArgs args,
        int retryAttempt)
    {
        var source = args.BasicProperties;
        var headers = source.Headers is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(source.Headers, StringComparer.Ordinal);
        headers[RetryAttemptHeader] = retryAttempt;

        return new BasicProperties
        {
            Persistent = true,
            ContentType = source.ContentType,
            ContentEncoding = source.ContentEncoding,
            MessageId = source.MessageId,
            CorrelationId = source.CorrelationId,
            Type = source.Type,
            Timestamp = source.Timestamp,
            Headers = headers
        };
    }

    private static int GetRetryAttempt(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(RetryAttemptHeader, out var value) || value is null)
        {
            return 0;
        }

        return value switch
        {
            int attempt => attempt,
            long attempt => Convert.ToInt32(attempt),
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var attempt) => attempt,
            string text when int.TryParse(text, out var attempt) => attempt,
            _ => 0
        };
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
