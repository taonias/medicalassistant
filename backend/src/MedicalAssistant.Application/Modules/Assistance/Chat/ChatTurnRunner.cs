using MedicalAssistant.Application.Contracts.ClinicalKnowledge;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Application.Features.Chat.Common;

/// <summary>
/// Runs a single grounded turn against the AI service and records the outcome on the
/// (already-persisted, tracked) assistant message. Shared by the ask and retry flows so
/// the context-building, AI call, and Completed/Refused/Failed transitions live in one place.
/// The answer is delivered whole — this never streams.
/// </summary>
public interface IChatTurnRunner
{
    Task RunAsync(
        Conversation conversation,
        string clinicalPatientId,
        string doctorId,
        ChatMessage userMessage,
        ChatMessage assistantMessage,
        CancellationToken cancellationToken);
}

public sealed class ChatTurnRunner : IChatTurnRunner
{
    /// <summary>The verbatim context window: the last N terminal messages replayed to the AI.</summary>
    public const int RecentWindow = 6;

    private const string GenericFailure =
        "The answer could not be generated. Please retry.";

    private readonly IConversationRepository _conversations;
    private readonly IGroundedAnswerGateway _clinicalKnowledge;
    private readonly ILogger<ChatTurnRunner> _logger;

    public ChatTurnRunner(
        IConversationRepository conversations,
        IGroundedAnswerGateway clinicalKnowledge,
        ILogger<ChatTurnRunner> logger)
    {
        _conversations = conversations;
        _clinicalKnowledge = clinicalKnowledge;
        _logger = logger;
    }

    public async Task RunAsync(
        Conversation conversation,
        string clinicalPatientId,
        string doctorId,
        ChatMessage userMessage,
        ChatMessage assistantMessage,
        CancellationToken cancellationToken)
    {
        var recent = await _conversations.GetRecentMessagesAsync(
            conversation.Id, assistantMessage.Sequence, RecentWindow, cancellationToken);

        var recentTurns = recent
            .Select(m => new ClinicalKnowledgeConversationTurn(
                m.Role == MessageRole.User ? "user" : "assistant",
                m.Content))
            .ToList();

        try
        {
            var answer = await _clinicalKnowledge.GetGroundedAnswerAsync(
                new ClinicalKnowledgeChatRequest(
                    PatientId: clinicalPatientId,
                    DoctorId: doctorId,
                    Question: userMessage.Content,
                    RecentTurns: recentTurns,
                    PriorSummary: conversation.RollingSummary,
                    AskId: assistantMessage.AskId),
                cancellationToken);

            if (answer.Refused)
            {
                // A refusal (insufficient evidence) is a normal answer, not a failure.
                assistantMessage.MarkRefused(answer.Text, NullIfBlank(answer.Language));
            }
            else
            {
                assistantMessage.MarkCompleted(
                    answer.Text,
                    NullIfBlank(answer.Language),
                    answer.Citations.Select(ToEntity));
            }
        }
        catch (Exception ex)
        {
            // Persist the failure with a doctor-safe reason; the doctor retries manually (no auto-retry).
            _logger.LogError(
                ex,
                "Chat turn {AskId} failed for conversation {ConversationId}",
                assistantMessage.AskId,
                conversation.Id);
            assistantMessage.MarkFailed(GenericFailure);
        }

        await _conversations.SaveChangesAsync(cancellationToken);
    }

    private static MessageCitation ToEntity(ClinicalKnowledgeCitation c) => new()
    {
        Label = c.Label,
        ChunkId = c.ChunkId,
        DocumentId = c.DocumentId,
        DocumentType = c.DocumentType,
        SessionId = c.SessionId,
        DocumentDate = c.DocumentDate,
        SourceRef = c.SourceRef,
        Quote = c.Quote,
        Score = c.Score,
    };

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
