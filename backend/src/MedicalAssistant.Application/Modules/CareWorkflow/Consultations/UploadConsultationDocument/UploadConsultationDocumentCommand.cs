using MediatR;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetConsultationDetails;
using Microsoft.AspNetCore.Http;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations.UploadConsultationDocument;

public class UploadConsultationDocumentCommand
    : IRequest<ConsultationDto>, IAuditableRequest<ConsultationDto>
{
    public int ConsultationId { get; set; }
    public required IFormFile DocumentFile { get; set; }

    public AuditEntry ToAuditEntry(ConsultationDto response) =>
        new("UploadDocument", "Consultation", ConsultationId.ToString());
}
