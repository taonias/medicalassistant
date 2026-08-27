using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Modules.Assistance.Chat;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MediatR;

namespace MedicalAssistant.Application.Modules.Assistance.Chat.AskChat;

/// <summary>
/// Ask one grounded turn. Unified entry point: with a <see cref="ConversationId"/> the turn
/// is appended; without one a conversation is auto-created and auto-titled from the question.
/// </summary>
public sealed class AskChatCommand : IRequest<AskChatResponse>, IAuditableRequest<AskChatResponse>
{
    /// <summary>Existing conversation to append to; null auto-creates one.</summary>
    public int? ConversationId { get; set; }

    /// <summary>Required when auto-creating without a consultation.</summary>
    public int? PatientId { get; set; }

    /// <summary>Optional: auto-create the conversation from a consultation (resolves its patient).</summary>
    public int? ConsultationId { get; set; }

    public required string Question { get; set; }

    /// <summary>Client-generated correlation + idempotency key; server-generated when omitted.</summary>
    public Guid AskId { get; set; }

    public AuditEntry ToAuditEntry(AskChatResponse response) =>
        new("AskChat", "Conversation", response.ConversationId.ToString(),
            $"MessageId: {response.MessageId}, State: {response.State}");
}

public sealed class AskChatCommandHandler : IRequestHandler<AskChatCommand, AskChatResponse>
{
    private readonly IConversationRepository _conversations;
    private readonly IPatientRepository _patients;
    private readonly IConsultationRepository _consultations;
    private readonly IUserService _userService;
    private readonly IChatTurnRunner _turnRunner;
    private readonly IConversationSummaryRefreshQueue _summaryRefreshQueue;
    private readonly IChatProgressNotifier _progress;

    public AskChatCommandHandler(
        IConversationRepository conversations,
        IPatientRepository patients,
        IConsultationRepository consultations,
        IUserService userService,
        IChatTurnRunner turnRunner,
        IConversationSummaryRefreshQueue summaryRefreshQueue,
        IChatProgressNotifier progress)
    {
        _conversations = conversations;
        _patients = patients;
        _consultations = consultations;
        _userService = userService;
        _turnRunner = turnRunner;
        _summaryRefreshQueue = summaryRefreshQueue;
        _progress = progress;
    }

    public async Task<AskChatResponse> Handle(AskChatCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var askId = request.AskId == default ? Guid.NewGuid() : request.AskId;

        // Idempotency: a turn already recorded for this askId is returned as-is (retry has its own path).
        var existing = await _conversations.GetAssistantByAskIdForDoctorAsync(askId, doctorId, cancellationToken);
        if (existing is not null)
        {
            var owningConversation = await _conversations.GetForDoctorAsync(existing.ConversationId, doctorId, cancellationToken);
            if (owningConversation is not null)
            {
                return existing.ToAskResponse(owningConversation);
            }
        }

        await _progress.NotifyAsync(doctorId,
            ChatProgressEvent.Of(askId, ChatPhases.ResolvingPatient, "Resolving patient…"), cancellationToken);

        var (conversation, patient) = await ResolveConversationAsync(request, doctorId, cancellationToken);

        var nextSequence = await _conversations.GetNextSequenceAsync(conversation.Id, cancellationToken);

        // First turn names the thread (covers both auto-create and an explicit New conversation).
        if (nextSequence == 1)
        {
            conversation.Rename(ConversationTitle.FromQuestion(request.Question));
        }

        var userMessage = new ChatMessage
        {
            ConversationId = conversation.Id,
            Sequence = nextSequence,
            Role = MessageRole.User,
            Content = request.Question,
            State = MessageState.Completed,
            AskId = askId,
        };

        var assistantMessage = new ChatMessage
        {
            ConversationId = conversation.Id,
            Sequence = nextSequence + 1,
            Role = MessageRole.Assistant,
            State = MessageState.Pending,
            AskId = askId,
        };

        await _conversations.AddMessagesAsync(cancellationToken, userMessage, assistantMessage);

        await _progress.NotifyAsync(doctorId,
            ChatProgressEvent.Of(askId, ChatPhases.PreparingContext, "Preparing context…"), cancellationToken);

        await _turnRunner.RunAsync(
            conversation,
            ClinicalPatientId.For(patient),
            doctorId,
            userMessage,
            assistantMessage,
            cancellationToken);

        // Off the critical path: maybe refresh the rolling summary for the next turn.
        _summaryRefreshQueue.Enqueue(conversation.Id);

        return assistantMessage.ToAskResponse(conversation);
    }

    private async Task<(Conversation Conversation, Domain.Patient Patient)> ResolveConversationAsync(
        AskChatCommand request, string doctorId, CancellationToken cancellationToken)
    {
        if (request.ConversationId is int conversationId)
        {
            var conversation = await _conversations.GetForDoctorAsync(conversationId, doctorId, cancellationToken)
                ?? throw new NotFoundException(nameof(Conversation), conversationId);

            var patient = await _patients.GetPatientForDoctorAsync(conversation.PatientId, doctorId)
                ?? throw new NotFoundException(nameof(Domain.Patient), conversation.PatientId);

            return (conversation, patient);
        }

        var resolvedPatient = await ResolvePatientForNewConversationAsync(request, doctorId);

        var created = new Conversation
        {
            DoctorId = doctorId,
            PatientId = resolvedPatient.Patient.Id,
            ConsultationId = resolvedPatient.ConsultationId,
            Title = ConversationTitle.FromQuestion(request.Question),
            Status = ConversationStatus.Active,
        };

        await _conversations.CreateAsync(created);
        return (created, resolvedPatient.Patient);
    }

    private async Task<(Domain.Patient Patient, int? ConsultationId)> ResolvePatientForNewConversationAsync(
        AskChatCommand request, string doctorId)
    {
        if (request.ConsultationId is int consultationId)
        {
            var consultation = await _consultations.GetConsultationForDoctorAsync(consultationId, doctorId)
                ?? throw new NotFoundException(nameof(Domain.Consultation), consultationId);

            if (!consultation.PatientId.HasValue)
            {
                throw new BadRequestException("Cannot start a conversation from a consultation without an assigned patient.");
            }

            var patient = await _patients.GetPatientForDoctorAsync(consultation.PatientId.Value, doctorId)
                ?? throw new NotFoundException(nameof(Domain.Patient), consultation.PatientId.Value);

            return (patient, consultation.Id);
        }

        if (request.PatientId is int patientId)
        {
            var patient = await _patients.GetPatientForDoctorAsync(patientId, doctorId)
                ?? throw new NotFoundException(nameof(Domain.Patient), patientId);

            return (patient, null);
        }

        throw new BadRequestException("A patientId or consultationId is required to start a conversation.");
    }
}
