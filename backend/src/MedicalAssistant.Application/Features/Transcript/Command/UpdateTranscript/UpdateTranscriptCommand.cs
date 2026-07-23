using MedicalAssistant.Application.Features.Transcript.Queries.GetTranscript;
using MediatR;

namespace MedicalAssistant.Application.Features.Transcript.Command.UpdateTranscript;

public class UpdateTranscriptCommand : IRequest<TranscriptDto>
{
    public int ConsultationId { get; set; }
    public required string Transcript { get; set; }
}
