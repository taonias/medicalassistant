using MediatR;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetConsultationDetails;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations.AssignConsultationPatient;

public class AssignConsultationPatientCommand
    : IRequest<ConsultationDto>, IAuditableRequest<ConsultationDto>
{
    public int ConsultationId { get; set; }
    public int PatientId { get; set; }

    public AuditEntry ToAuditEntry(ConsultationDto response) =>
        new("AssignConsultationPatient", "Consultation", ConsultationId.ToString(),
            $"PatientId: {PatientId}");
}
