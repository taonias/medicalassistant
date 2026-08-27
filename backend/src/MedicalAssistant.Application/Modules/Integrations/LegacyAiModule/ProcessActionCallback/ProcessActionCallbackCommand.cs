using MediatR;
using MedicalAssistant.Application.Contracts.Logging;

namespace MedicalAssistant.Application.Modules.Integrations.LegacyAiModule.ProcessActionCallback;

public class ProcessActionCallbackCommand : IRequest<Unit>, IAuditableRequest<Unit>
{
    public required string JobId { get; set; }
    public required string CorrelationId { get; set; }
    public required string Status { get; set; }
    public string? ResponsePayload { get; set; }
    public string? FailureReason { get; set; }

    public AuditEntry ToAuditEntry(Unit response) =>
        new("ActionCallback", "ActionRequest", CorrelationId,
            $"Status: {Status}",
            SystemAuditActors.AiModuleId, SystemAuditActors.AiModuleName);
}
