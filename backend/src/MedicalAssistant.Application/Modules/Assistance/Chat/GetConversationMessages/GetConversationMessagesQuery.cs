using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Modules.Assistance.Chat;
using MedicalAssistant.Domain;
using MediatR;

namespace MedicalAssistant.Application.Modules.Assistance.Chat.GetConversationMessages;

/// <summary>A full conversation thread (header + messages + structured citations) for rehydration.</summary>
public sealed record GetConversationMessagesQuery(int ConversationId) : IRequest<ConversationThreadDto>;

public sealed class GetConversationMessagesQueryHandler
    : IRequestHandler<GetConversationMessagesQuery, ConversationThreadDto>
{
    private readonly IConversationRepository _conversations;
    private readonly IUserService _userService;

    public GetConversationMessagesQueryHandler(IConversationRepository conversations, IUserService userService)
    {
        _conversations = conversations;
        _userService = userService;
    }

    public async Task<ConversationThreadDto> Handle(
        GetConversationMessagesQuery request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var conversation = await _conversations.GetForDoctorAsync(request.ConversationId, doctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), request.ConversationId);

        var messages = await _conversations.GetMessagesForDoctorAsync(request.ConversationId, doctorId, cancellationToken);

        return new ConversationThreadDto
        {
            Conversation = conversation.ToSummaryDto(),
            Messages = messages.Select(m => m.ToDto()).ToList(),
        };
    }
}
