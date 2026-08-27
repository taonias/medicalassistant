using MediatR;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Modules.Integrations.LegacyAiModule.GetActionRequestStatus;
using MedicalAssistant.Domain.Enums;

namespace MedicalAssistant.Application.Modules.Integrations.LegacyAiModule.TriggerAiAction;

public class TriggerAiActionCommand
    : IRequest<ActionRequestDto>, IAuditableRequest<ActionRequestDto>
{
    public ActionType ActionType { get; set; }
    public int? PatientId { get; set; }
    public int? ConsultationId { get; set; }
    public string? ParametersJson { get; set; }
    public string? CorrelationId { get; set; }

    public AuditEntry ToAuditEntry(ActionRequestDto response) =>
        new("TriggerAction", "ActionRequest", response.CorrelationId,
            $"ActionType: {response.ActionType}");
}
