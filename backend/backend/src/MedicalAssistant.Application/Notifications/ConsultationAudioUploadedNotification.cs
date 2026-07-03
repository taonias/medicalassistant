using MediatR;

namespace MedicalAssistant.Application.Notifications;

public class ConsultationAudioUploadedNotification : INotification
{
    public int ConsultationId { get; set; }
    public required string DoctorId { get; set; }
    public required string AudioBlobUri { get; set; }
    public required string CorrelationId { get; set; }
}
