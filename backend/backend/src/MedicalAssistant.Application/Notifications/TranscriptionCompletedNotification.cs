using MediatR;

namespace MedicalAssistant.Application.Notifications;

public class TranscriptionCompletedNotification : INotification
{
    public int ConsultationId { get; set; }
    public int TranscriptId { get; set; }
    public required string TranscriptText { get; set; }
    public required string CorrelationId { get; set; }
}
