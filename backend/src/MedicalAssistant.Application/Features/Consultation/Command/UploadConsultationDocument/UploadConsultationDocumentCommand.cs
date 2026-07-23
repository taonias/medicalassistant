using MediatR;
using Microsoft.AspNetCore.Http;

namespace MedicalAssistant.Application.Features.Consultation.Command.UploadConsultationDocument;

public class UploadConsultationDocumentCommand : IRequest<Queries.GetConsultationDetails.ConsultationDto>
{
    public int ConsultationId { get; set; }
    public required IFormFile DocumentFile { get; set; }
}
