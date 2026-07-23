using MediatR;

namespace MedicalAssistant.Application.Features.Transcript.Command.ProcessTranscriptionCallback;

public class ProcessTranscriptionCallbackCommand : IRequest<Unit>
{
    public required string JobId { get; set; }
    public required string CorrelationId { get; set; }
    public int ConsultationId { get; set; }
    public required string Status { get; set; }
    public string? Transcript { get; set; }
    public string? FailureReason { get; set; }
}
