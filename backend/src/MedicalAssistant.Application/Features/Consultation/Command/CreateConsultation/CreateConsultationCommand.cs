using MediatR;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;

namespace MedicalAssistant.Application.Features.Consultation.Command.CreateConsultation;

public class CreateConsultationCommand
    : IRequest<ConsultationDto>, IAuditableRequest<ConsultationDto>
{
    public int? PatientId { get; set; }
    public DateTime ConsultationDate { get; set; } = DateTime.UtcNow;
    public int? DurationSeconds { get; set; }
    public string? IdempotencyKey { get; set; }

    public AuditEntry ToAuditEntry(ConsultationDto response) =>
        new("CreateConsultation", "Consultation", response.Id.ToString());
}
