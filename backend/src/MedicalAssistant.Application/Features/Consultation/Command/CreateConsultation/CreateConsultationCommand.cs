using MediatR;
using MedicalAssistant.Domain.Enums;

namespace MedicalAssistant.Application.Features.Consultation.Command.CreateConsultation;

public class CreateConsultationCommand : IRequest<Queries.GetConsultationDetails.ConsultationDto>
{
    public int? PatientId { get; set; }
    public DateTime ConsultationDate { get; set; } = DateTime.UtcNow;
    public int? DurationSeconds { get; set; }
    public string? IdempotencyKey { get; set; }
}
