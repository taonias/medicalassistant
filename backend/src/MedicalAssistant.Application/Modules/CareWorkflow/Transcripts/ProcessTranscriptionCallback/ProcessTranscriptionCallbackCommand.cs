using MediatR;
using MedicalAssistant.Application.Contracts.Logging;

namespace MedicalAssistant.Application.Features.Transcript.Command.ProcessTranscriptionCallback;

public class ProcessTranscriptionCallbackCommand : IRequest<Unit>, IAuditableRequest<Unit>
{
    public required string JobId { get; set; }
    public required string CorrelationId { get; set; }
    public int ConsultationId { get; set; }
    public required string Status { get; set; }
    public string? Transcript { get; set; }
    public string? FailureReason { get; set; }

    public AuditEntry ToAuditEntry(Unit response) =>
        new("TranscriptionCallback", "Consultation", ConsultationId.ToString(),
            $"Status: {Status}",
            SystemAuditActors.AiModuleId, SystemAuditActors.AiModuleName);
}
