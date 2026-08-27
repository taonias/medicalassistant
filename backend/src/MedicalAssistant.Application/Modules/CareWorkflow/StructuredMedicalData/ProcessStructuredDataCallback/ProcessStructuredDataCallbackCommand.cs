using MediatR;
using MedicalAssistant.Application.Contracts.Logging;

namespace MedicalAssistant.Application.Modules.CareWorkflow.StructuredMedicalData.ProcessStructuredDataCallback;

public class ProcessStructuredDataCallbackCommand : IRequest<Unit>, IAuditableRequest<Unit>
{
    public required string JobId { get; set; }
    public required string CorrelationId { get; set; }
    public int ConsultationId { get; set; }
    public int? TranscriptId { get; set; }
    public required string SchemaVersion { get; set; }
    public required string StructuredPayload { get; set; }
    public required string Status { get; set; }
    public string? FailureReason { get; set; }

    public AuditEntry ToAuditEntry(Unit response) =>
        new("StructuredDataCallback", "Consultation", ConsultationId.ToString(),
            $"Status: {Status}",
            SystemAuditActors.AiModuleId, SystemAuditActors.AiModuleName);
}
