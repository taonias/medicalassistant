using MediatR;
using MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetConsultationsByPatient;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetDraftConsultations;

public record GetDraftConsultationsQuery : IRequest<List<DraftConsultationGroupDto>>;

public class DraftConsultationGroupDto
{
    public int PatientId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public List<ConsultationSummaryDto> Consultations { get; set; } = [];
}
