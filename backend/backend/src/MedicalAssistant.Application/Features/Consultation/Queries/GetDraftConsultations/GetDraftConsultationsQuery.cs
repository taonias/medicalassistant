using MediatR;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationsByPatient;

namespace MedicalAssistant.Application.Features.Consultation.Queries.GetDraftConsultations;

public record GetDraftConsultationsQuery : IRequest<List<DraftConsultationGroupDto>>;

public class DraftConsultationGroupDto
{
    public int PatientId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public List<ConsultationSummaryDto> Consultations { get; set; } = [];
}
