using FluentValidation;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Modules.Assistance.Chat;
using MedicalAssistant.Domain;
using MediatR;

namespace MedicalAssistant.Application.Modules.Assistance.Chat.RenameConversation;

public sealed class RenameConversationCommand : IRequest<ConversationSummaryDto>, IAuditableRequest<ConversationSummaryDto>
{
    public int ConversationId { get; set; }
    public required string Title { get; set; }

    public AuditEntry ToAuditEntry(ConversationSummaryDto response) =>
        new("RenameConversation", "Conversation", response.Id.ToString(), null);
}

public sealed class RenameConversationCommandHandler
    : IRequestHandler<RenameConversationCommand, ConversationSummaryDto>
{
    private readonly IConversationRepository _conversations;
    private readonly IUserService _userService;

    public RenameConversationCommandHandler(IConversationRepository conversations, IUserService userService)
    {
        _conversations = conversations;
        _userService = userService;
    }

    public async Task<ConversationSummaryDto> Handle(
        RenameConversationCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var conversation = await _conversations.GetForDoctorAsync(request.ConversationId, doctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), request.ConversationId);

        conversation.Rename(request.Title.Trim());
        await _conversations.SaveChangesAsync(cancellationToken);

        return conversation.ToSummaryDto();
    }
}

public sealed class RenameConversationCommandValidator : AbstractValidator<RenameConversationCommand>
{
    public RenameConversationCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(200);
    }
}
