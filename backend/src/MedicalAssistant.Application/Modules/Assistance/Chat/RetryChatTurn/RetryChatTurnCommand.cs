using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Features.Chat.Common;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MediatR;

namespace MedicalAssistant.Application.Features.Chat.Command.RetryChatTurn;

/// <summary>Doctor-triggered manual retry of a failed turn. Re-runs it in place, reusing the askId.</summary>
public sealed class RetryChatTurnCommand : IRequest<AskChatResponse>, IAuditableRequest<AskChatResponse>
{
    public int ConversationId { get; set; }
    public int MessageId { get; set; }

    public AuditEntry ToAuditEntry(AskChatResponse response) =>
        new("RetryChatTurn", "Conversation", response.ConversationId.ToString(),
            $"MessageId: {response.MessageId}, State: {response.State}");
}

public sealed class RetryChatTurnCommandHandler : IRequestHandler<RetryChatTurnCommand, AskChatResponse>
{
    private readonly IConversationRepository _conversations;
    private readonly IPatientRepository _patients;
    private readonly IUserService _userService;
    private readonly IChatTurnRunner _turnRunner;
    private readonly IConversationSummaryRefreshQueue _summaryRefreshQueue;

    public RetryChatTurnCommandHandler(
        IConversationRepository conversations,
        IPatientRepository patients,
        IUserService userService,
        IChatTurnRunner turnRunner,
        IConversationSummaryRefreshQueue summaryRefreshQueue)
    {
        _conversations = conversations;
        _patients = patients;
        _userService = userService;
        _turnRunner = turnRunner;
        _summaryRefreshQueue = summaryRefreshQueue;
    }

    public async Task<AskChatResponse> Handle(RetryChatTurnCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var assistant = await _conversations.GetTrackedMessageForDoctorAsync(
                request.ConversationId, request.MessageId, doctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(ChatMessage), request.MessageId);

        if (assistant.Role != MessageRole.Assistant)
        {
            throw new BadRequestException("Only an assistant turn can be retried.");
        }

        if (assistant.State != MessageState.Failed)
        {
            throw new BadRequestException("Only a failed turn can be retried.");
        }

        var conversation = await _conversations.GetForDoctorAsync(request.ConversationId, doctorId, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), request.ConversationId);

        var patient = await _patients.GetPatientForDoctorAsync(conversation.PatientId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Patient), conversation.PatientId);

        var userMessage = await _conversations.GetUserMessageByAskIdAsync(
                request.ConversationId, assistant.AskId, cancellationToken)
            ?? throw new BadRequestException("The original question for this turn could not be found.");

        assistant.ResetForRetry();
        await _conversations.SaveChangesAsync(cancellationToken);

        await _turnRunner.RunAsync(
            conversation,
            ClinicalPatientId.For(patient),
            doctorId,
            userMessage,
            assistant,
            cancellationToken);

        // A now-succeeded turn may have pushed the thread past the summary window.
        _summaryRefreshQueue.Enqueue(conversation.Id);

        return assistant.ToAskResponse(conversation);
    }
}
