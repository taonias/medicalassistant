using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Models.Messaging;
using MedicalAssistant.EventBusRabbitMQ;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Infrastructure.Messaging;

public sealed class RabbitMqIntegrationEventReplayPublisher : IIntegrationEventReplayPublisher
{
    private readonly RabbitMqConfirmedPublisher _publisher;
    private readonly RabbitMqPublishOptions _options;

    public RabbitMqIntegrationEventReplayPublisher(
        RabbitMqConfirmedPublisher publisher,
        IOptions<RabbitMqPublishOptions> options)
    {
        _publisher = publisher;
        _options = options.Value;
    }

    public async Task PublishAsync(
        IntegrationEventReplayMessage message,
        CancellationToken cancellationToken = default)
    {
        var request = RabbitMqPublishRequestFactory.CreateReplay(
            message.EventId,
            message.EventType,
            message.CorrelationId,
            message.EnvelopeJson,
            _options);

        await _publisher.PublishAsync(request, cancellationToken);
    }
}
