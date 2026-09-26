using MedicalAssistant.Application.Features.Chat.Queries.GetChatRequestStatus;
using MediatR;

namespace MedicalAssistant.Application.Features.Chat.Command.SubmitChatQuery;

public class SubmitChatQueryCommand : IRequest<ChatJobDto>
{
    public int? PatientId { get; set; }
    public required string Message { get; set; }
    public string? SessionId { get; set; }
}
