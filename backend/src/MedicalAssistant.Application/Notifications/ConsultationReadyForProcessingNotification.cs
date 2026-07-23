using MedicalAssistant.Application.Models.Messaging;
using MediatR;

namespace MedicalAssistant.Application.Notifications;

/// <summary>
/// Raised when a consultation file is ready for downstream processing
/// (upload completed, or an existing file was attached to a patient).
/// </summary>
public sealed class ConsultationReadyForProcessingNotification : INotification
{
    public required string EventType { get; init; }
    public int ConsultationId { get; init; }
    public int? PatientId { get; init; }
    public required string DoctorId { get; init; }
    public required string Status { get; init; }
    public string? FileType { get; init; }
    public string? BlobUri { get; init; }
    public string? ContentType { get; init; }
    public string? FileName { get; init; }
    public int? DurationSeconds { get; init; }
    public required string CorrelationId { get; init; }
}
