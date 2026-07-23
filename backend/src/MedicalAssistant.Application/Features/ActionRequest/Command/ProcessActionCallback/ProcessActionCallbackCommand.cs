using MediatR;

namespace MedicalAssistant.Application.Features.ActionRequest.Command.ProcessActionCallback;

public class ProcessActionCallbackCommand : IRequest<Unit>
{
    public required string JobId { get; set; }
    public required string CorrelationId { get; set; }
    public required string Status { get; set; }
    public string? ResponsePayload { get; set; }
    public string? FailureReason { get; set; }
}
