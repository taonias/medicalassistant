using MediatR;

namespace MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;

public record GetConsultationDetailsQuery(int Id) : IRequest<ConsultationDto>;

public class ConsultationDto
{
    public int Id { get; set; }
    public int? PatientId { get; set; }
    public required string DoctorId { get; set; }
    public DateTime ConsultationDate { get; set; }
    public required string Status { get; set; }
    public string? AudioBlobUri { get; set; }
    public string? AudioContentType { get; set; }
    public int? DurationSeconds { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? FailureReason { get; set; }
}
