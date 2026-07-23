using MediatR;

namespace MedicalAssistant.Application.Notifications;

/// <summary>
/// Audio is already stored and the consultation is marked AudioUploaded by the upload command.
/// Transcription is not started automatically from this notification.
/// </summary>
public class ConsultationAudioUploadedNotificationHandler
    : INotificationHandler<ConsultationAudioUploadedNotification>
{
    public Task Handle(ConsultationAudioUploadedNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
