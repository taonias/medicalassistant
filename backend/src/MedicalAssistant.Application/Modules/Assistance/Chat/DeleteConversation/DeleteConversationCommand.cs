using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Domain;
using MediatR;

namespace MedicalAssistant.Application.Modules.Assistance.Chat.DeleteConversation;

/// <summary>Soft-deletes (archives) a conversation. History is retained; it just leaves the active list.</summary>
public sealed class DeleteConversationCommand : IRequest<Unit>, IAuditableRequest<Unit>
{
    public int ConversationId { get; set; }

    public AuditEntry ToAuditEntry(Unit response) =>
        new("DeleteConversation", "Conversation", ConversationId.ToString(), null);
}

public sealed class DeleteConversationCommandHandler : IRequestHandler<DeleteConversationCommand, Unit>
{
    private readonly IConversationRepository _conversations;
    private readonly IUserService _userService;

    public DeleteConversationCommandHandler(IConversationRepository conversations, IUserService userService)
    {
        _conversations = conversations;
        _userService = userService;
    }

    public async Task<Unit> Handle(DeleteConversationCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var conversation = await _conversations.GetForDoctorAsync(request.ConversationId, doctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), request.ConversationId);

        conversation.Archive();
        await _conversations.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
