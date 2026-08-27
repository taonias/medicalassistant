using MediatR;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetConsultationDetails;
using Microsoft.AspNetCore.Http;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations.UploadConsultationAudio;

public class UploadConsultationAudioCommand
    : IRequest<ConsultationDto>, IAuditableRequest<ConsultationDto>
{
    public int ConsultationId { get; set; }
    public required IFormFile AudioFile { get; set; }
    public int? DurationSeconds { get; set; }

    public AuditEntry ToAuditEntry(ConsultationDto response) =>
        new("UploadAudio", "Consultation", ConsultationId.ToString());
}
