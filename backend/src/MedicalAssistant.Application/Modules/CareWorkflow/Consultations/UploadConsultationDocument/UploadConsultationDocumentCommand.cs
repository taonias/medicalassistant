using MediatR;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;
using Microsoft.AspNetCore.Http;

namespace MedicalAssistant.Application.Features.Consultation.Command.UploadConsultationDocument;

public class UploadConsultationDocumentCommand
    : IRequest<ConsultationDto>, IAuditableRequest<ConsultationDto>
{
    public int ConsultationId { get; set; }
    public required IFormFile DocumentFile { get; set; }

    public AuditEntry ToAuditEntry(ConsultationDto response) =>
        new("UploadDocument", "Consultation", ConsultationId.ToString());
}
