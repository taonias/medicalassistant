using MedicalAssistant.EventBus;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqConfirmedPublisher
{
    private readonly IRabbitMqPersistentConnection _connection;
    private readonly RabbitMqPublishOptions _options;

    public RabbitMqConfirmedPublisher(
        IRabbitMqPersistentConnection connection,
        IOptions<RabbitMqPublishOptions> options)
    {
        _connection = connection;
        _options = options.Value;
    }

    public async Task PublishAsync<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken)
    {
        var request = RabbitMqPublishRequestFactory.Create(envelope, _options);
        await PublishAsync(request, cancellationToken);
    }

    public async Task PublishAsync(
        RabbitMqPublishRequest request,
        CancellationToken cancellationToken)
    {
        await using var channel = await _connection.CreateChannelAsync(
            new CreateChannelOptions(true, true, null, null),
            cancellationToken);
        var sequenceNumber = await channel.GetNextPublishSequenceNumberAsync(cancellationToken);
        var confirm = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task OnAck(object sender, BasicAckEventArgs args)
        {
            if (CoversSequence(args.DeliveryTag, args.Multiple, sequenceNumber))
            {
                confirm.TrySetResult();
            }

            return Task.CompletedTask;
        }

        Task OnNack(object sender, BasicNackEventArgs args)
        {
            if (CoversSequence(args.DeliveryTag, args.Multiple, sequenceNumber))
            {
                confirm.TrySetException(new RabbitMqPublishException(
                    $"RabbitMQ negatively acknowledged event '{request.RoutingKey}'."));
            }

            return Task.CompletedTask;
        }

        Task OnReturn(object sender, BasicReturnEventArgs args)
        {
            confirm.TrySetException(new RabbitMqPublishException(
                $"RabbitMQ returned unroutable event '{request.RoutingKey}'."));
            return Task.CompletedTask;
        }

        channel.BasicAcksAsync += OnAck;
        channel.BasicNacksAsync += OnNack;
        channel.BasicReturnAsync += OnReturn;

        try
        {
            await channel.BasicPublishAsync(
                exchange: request.ExchangeName,
                routingKey: request.RoutingKey,
                mandatory: request.Mandatory,
                basicProperties: request.Properties,
                body: request.Body,
                cancellationToken: cancellationToken);

            var completed = await Task.WhenAny(
                confirm.Task,
                Task.Delay(_options.ConfirmTimeout, cancellationToken));
            if (completed != confirm.Task)
            {
                throw new RabbitMqPublishException(
                    $"RabbitMQ did not confirm event '{request.RoutingKey}' before the configured timeout.");
            }

            await confirm.Task;
        }
        finally
        {
            channel.BasicAcksAsync -= OnAck;
            channel.BasicNacksAsync -= OnNack;
            channel.BasicReturnAsync -= OnReturn;
        }
    }

    private static bool CoversSequence(
        ulong deliveryTag,
        bool multiple,
        ulong sequenceNumber) =>
        multiple ? sequenceNumber <= deliveryTag : sequenceNumber == deliveryTag;
}
