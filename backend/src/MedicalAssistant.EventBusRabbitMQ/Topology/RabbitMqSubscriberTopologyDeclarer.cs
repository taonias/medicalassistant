using RabbitMQ.Client;

namespace MedicalAssistant.EventBusRabbitMQ;

public sealed class RabbitMqSubscriberTopologyDeclarer
{
    public async Task DeclareAsync(
        IChannel channel,
        RabbitMqSubscriberTopologyPlan plan,
        CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(
            exchange: plan.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await DeclareQueueAsync(channel, plan.DeadLetterQueue, cancellationToken);

        foreach (var retryQueue in plan.RetryQueues)
        {
            await DeclareQueueAsync(channel, retryQueue, cancellationToken);
        }

        await DeclareQueueAsync(channel, plan.MainQueue, cancellationToken);

        foreach (var binding in plan.Bindings)
        {
            await channel.QueueBindAsync(
                queue: binding.QueueName,
                exchange: plan.ExchangeName,
                routingKey: binding.RoutingKey,
                arguments: null,
                cancellationToken: cancellationToken);
        }
    }

    private static async Task DeclareQueueAsync(
        IChannel channel,
        RabbitMqQueuePlan queue,
        CancellationToken cancellationToken)
    {
        var arguments = new Dictionary<string, object?>();
        if (queue.DeadLetterExchange is not null)
        {
            arguments["x-dead-letter-exchange"] = queue.DeadLetterExchange;
        }

        if (queue.DeadLetterRoutingKey is not null)
        {
            arguments["x-dead-letter-routing-key"] = queue.DeadLetterRoutingKey;
        }

        if (queue.MessageTtl is not null)
        {
            arguments["x-message-ttl"] = Convert.ToInt32(queue.MessageTtl.Value.TotalMilliseconds);
        }

        await channel.QueueDeclareAsync(
            queue: queue.Name,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments.Count == 0 ? null : arguments,
            cancellationToken: cancellationToken);
    }
}
