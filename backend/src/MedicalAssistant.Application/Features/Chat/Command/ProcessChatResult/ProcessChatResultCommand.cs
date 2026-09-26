using MediatR;

namespace MedicalAssistant.Application.Features.Chat.Command.ProcessChatResult;

public class ProcessChatResultCommand : IRequest<Unit>
{
    public required string CorrelationId { get; set; }
    public required string Status { get; set; }
    public string? Answer { get; set; }
    public List<string> Citations { get; set; } = [];
    public List<string> SuggestedActions { get; set; } = [];
    public string? FailureReason { get; set; }
}
