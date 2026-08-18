using MediatR;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;

namespace MedicalAssistant.Application.Features.Consultation.Command.AssignConsultationPatient;

public class AssignConsultationPatientCommand
    : IRequest<ConsultationDto>, IAuditableRequest<ConsultationDto>
{
    public int ConsultationId { get; set; }
    public int PatientId { get; set; }

    public AuditEntry ToAuditEntry(ConsultationDto response) =>
        new("AssignConsultationPatient", "Consultation", ConsultationId.ToString(),
            $"PatientId: {PatientId}");
}
