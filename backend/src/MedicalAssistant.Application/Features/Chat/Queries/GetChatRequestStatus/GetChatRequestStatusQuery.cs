using MediatR;

namespace MedicalAssistant.Application.Features.Chat.Queries.GetChatRequestStatus;

public record GetChatRequestStatusQuery(string CorrelationId) : IRequest<ChatJobDto>;

public class ChatJobDto
{
    public required string CorrelationId { get; set; }
    public required string Status { get; set; }
    public string? Answer { get; set; }
    public List<string> Citations { get; set; } = [];
    public List<string> SuggestedActions { get; set; } = [];
    public string? FailureReason { get; set; }
}
