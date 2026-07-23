using MediatR;
using Microsoft.AspNetCore.Http;

namespace MedicalAssistant.Application.Features.Consultation.Command.UploadConsultationAudio;

public class UploadConsultationAudioCommand : IRequest<Queries.GetConsultationDetails.ConsultationDto>
{
    public int ConsultationId { get; set; }
    public required IFormFile AudioFile { get; set; }
    public int? DurationSeconds { get; set; }
}
