using MedicalAssistant.Domain.Common;
using MedicalAssistant.Domain.Enums;

namespace MedicalAssistant.Domain;

/// <summary>
/// One turn in a <see cref="Conversation"/> — either the doctor's question
/// (<see cref="MessageRole.User"/>) or the grounded answer
/// (<see cref="MessageRole.Assistant"/>). An assistant turn moves through the
/// <see cref="MessageState"/> lifecycle and carries its verified citations.
/// </summary>
public class ChatMessage : BaseEntity
{
    public int ConversationId { get; set; }

    /// <summary>Monotonic order within the conversation (1-based).</summary>
    public int Sequence { get; set; }

    public MessageRole Role { get; set; }

    /// <summary>
    /// The question, or the answer prose (with inline <c>[E#]</c> markers).
    /// Empty while an assistant turn is <see cref="MessageState.Pending"/>.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    public MessageState State { get; set; } = MessageState.Pending;

    /// <summary>Set only on <see cref="MessageState.Failed"/>.</summary>
    public string? FailureReason { get; set; }

    /// <summary>Language of the answer (assistant turns).</summary>
    public string? Language { get; set; }

    /// <summary>Correlation + idempotency key for the turn; a retry reuses it.</summary>
    public Guid AskId { get; set; }

    public ICollection<MessageCitation> Citations { get; set; } = new List<MessageCitation>();

    public void MarkCompleted(string content, string? language, IEnumerable<MessageCitation> citations)
    {
        Content = content;
        Language = language;
        State = MessageState.Completed;
        FailureReason = null;
        Citations = citations.ToList();
    }

    public void MarkRefused(string content, string? language)
    {
        Content = content;
        Language = language;
        State = MessageState.Refused;
        FailureReason = null;
        Citations = new List<MessageCitation>();
    }

    public void MarkFailed(string reason)
    {
        State = MessageState.Failed;
        FailureReason = reason;
    }

    /// <summary>Returns a failed turn to <see cref="MessageState.Pending"/> for a doctor-triggered retry.</summary>
    public void ResetForRetry()
    {
        State = MessageState.Pending;
        FailureReason = null;
    }
}
