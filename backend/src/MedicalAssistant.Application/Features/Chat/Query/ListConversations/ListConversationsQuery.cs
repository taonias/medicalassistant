using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Features.Chat.Common;
using MediatR;

namespace MedicalAssistant.Application.Features.Chat.Query.ListConversations;

/// <summary>A doctor's active conversations about one patient (history list).</summary>
public sealed record ListConversationsQuery(int PatientId) : IRequest<IReadOnlyList<ConversationSummaryDto>>;

public sealed class ListConversationsQueryHandler
    : IRequestHandler<ListConversationsQuery, IReadOnlyList<ConversationSummaryDto>>
{
    private readonly IConversationRepository _conversations;
    private readonly IUserService _userService;

    public ListConversationsQueryHandler(IConversationRepository conversations, IUserService userService)
    {
        _conversations = conversations;
        _userService = userService;
    }

    public async Task<IReadOnlyList<ConversationSummaryDto>> Handle(
        ListConversationsQuery request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var conversations = await _conversations.ListByPatientForDoctorAsync(
            request.PatientId, doctorId, cancellationToken);

        return conversations.Select(c => c.ToSummaryDto()).ToList();
    }
}
