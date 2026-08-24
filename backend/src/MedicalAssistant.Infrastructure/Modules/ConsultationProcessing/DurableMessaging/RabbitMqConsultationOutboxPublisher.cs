using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Domain;
using MedicalAssistant.EventBusRabbitMQ;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Infrastructure.Messaging;

public sealed class RabbitMqConsultationOutboxPublisher : IConsultationOutboxPublisher
{
    private readonly RabbitMqConfirmedPublisher _publisher;
    private readonly RabbitMqPublishOptions _options;

    public RabbitMqConsultationOutboxPublisher(
        RabbitMqConfirmedPublisher publisher,
        IOptions<RabbitMqPublishOptions> options)
    {
        _publisher = publisher;
        _options = options.Value;
    }

    public async Task PublishAsync(
        ConsultationOutboxMessage message,
        CancellationToken cancellationToken = default)
    {
        var request = RabbitMqPublishRequestFactory.CreateFromOutbox(
            message.EventId,
            message.EventType,
            message.EventVersion,
            message.OccurredAtUtc,
            message.Producer,
            message.CorrelationId,
            message.CausationId,
            message.Payload,
            _options);

        await _publisher.PublishAsync(request, cancellationToken);
    }
}
