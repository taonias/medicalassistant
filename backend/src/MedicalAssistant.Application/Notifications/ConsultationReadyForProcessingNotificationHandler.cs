using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Models.Messaging;
using MediatR;

namespace MedicalAssistant.Application.Notifications;

public sealed class ConsultationReadyForProcessingNotificationHandler
    : INotificationHandler<ConsultationReadyForProcessingNotification>
{
    private readonly IConsultationProcessingPublisher _publisher;
    private readonly IAppLogger<ConsultationReadyForProcessingNotificationHandler> _logger;

    public ConsultationReadyForProcessingNotificationHandler(
        IConsultationProcessingPublisher publisher,
        IAppLogger<ConsultationReadyForProcessingNotificationHandler> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(
        ConsultationReadyForProcessingNotification notification,
        CancellationToken cancellationToken)
    {
        var message = new ConsultationProcessingMessage
        {
            EventType = notification.EventType,
            ConsultationId = notification.ConsultationId,
            PatientId = notification.PatientId,
            DoctorId = notification.DoctorId,
            Status = notification.Status,
            FileType = notification.FileType,
            BlobUri = notification.BlobUri,
            ContentType = notification.ContentType,
            FileName = notification.FileName,
            DurationSeconds = notification.DurationSeconds,
            CorrelationId = notification.CorrelationId,
            OccurredAtUtc = DateTime.UtcNow,
        };

        try
        {
            await _publisher.PublishAsync(message, cancellationToken);
            _logger.LogInformation(
                "Published {EventType} for consultation {ConsultationId} (correlation {CorrelationId}).",
                message.EventType,
                message.ConsultationId,
                message.CorrelationId);
        }
        catch (Exception ex)
        {
            // Do not fail the user-facing command if the broker is temporarily unavailable.
            _logger.LogWarning(
                "Failed to publish {EventType} for consultation {ConsultationId}: {Error}",
                message.EventType,
                message.ConsultationId,
                ex.Message);
        }
    }
}
