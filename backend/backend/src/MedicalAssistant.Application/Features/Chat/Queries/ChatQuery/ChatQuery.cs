using MediatR;

namespace MedicalAssistant.Application.Features.Chat.Queries.ChatQuery;

public class ChatQuery : IRequest<ChatResponseDto>
{
    public int? PatientId { get; set; }
    public int? ConsultationId { get; set; }
    public required string Message { get; set; }
    public string? SessionId { get; set; }
}

public class ChatResponseDto
{
    public required string Answer { get; set; }
    public List<string> Citations { get; set; } = [];
    public List<string> SuggestedActions { get; set; } = [];
}
