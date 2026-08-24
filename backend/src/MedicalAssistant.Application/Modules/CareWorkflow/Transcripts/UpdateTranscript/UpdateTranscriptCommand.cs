using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Features.Transcript.Queries.GetTranscript;
using MediatR;

namespace MedicalAssistant.Application.Features.Transcript.Command.UpdateTranscript;

public class UpdateTranscriptCommand : IRequest<TranscriptDto>, IAuditableRequest<TranscriptDto>
{
    public int ConsultationId { get; set; }
    public required string Transcript { get; set; }

    public AuditEntry ToAuditEntry(TranscriptDto response) =>
        new("UpdateTranscript", "Transcript", ConsultationId.ToString());
}
