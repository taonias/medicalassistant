using MediatR;
using MedicalAssistant.Application.Contracts.Logging;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations.DeleteConsultation;

public record DeleteConsultationCommand(int Id) : IRequest<Unit>, IAuditableRequest<Unit>
{
    public AuditEntry ToAuditEntry(Unit response) =>
        new("DeleteConsultation", "Consultation", Id.ToString());
}
