using MediatR;
using MedicalAssistant.Domain.Enums;

namespace MedicalAssistant.Application.Features.ActionRequest.Command.TriggerAiAction;

public class TriggerAiActionCommand : IRequest<Queries.GetActionRequestStatus.ActionRequestDto>
{
    public ActionType ActionType { get; set; }
    public int? PatientId { get; set; }
    public int? ConsultationId { get; set; }
    public string? ParametersJson { get; set; }
    public string? CorrelationId { get; set; }
}
