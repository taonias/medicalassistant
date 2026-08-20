using MedicalAssistant.Domain.Common;
using MedicalAssistant.Domain.Enums;

namespace MedicalAssistant.Domain;

/// <summary>
/// A durable doctor↔AI conversation about one patient. The AI service stays
/// stateless (ADR-0010); all conversation history lives here. Scope is one
/// doctor + one patient, optionally tagged with a consultation.
/// </summary>
public class Conversation : BaseEntity
{
    public required string DoctorId { get; set; }
    public int PatientId { get; set; }

    /// <summary>Optional link to the consultation the conversation was started from.</summary>
    public int? ConsultationId { get; set; }

    /// <summary>Auto-generated from the first question; renamable by the doctor.</summary>
    public required string Title { get; set; }

    /// <summary>
    /// Rolling summary of the older history that has scrolled out of the verbatim
    /// window. Null until the conversation grows past the window. It is
    /// phrasing/refinement input for the AI only — never evidence.
    /// </summary>
    public string? RollingSummary { get; set; }

    /// <summary>
    /// The highest message <see cref="ChatMessage.Sequence"/> the
    /// <see cref="RollingSummary"/> covers. Lets the refresh trigger tell how many
    /// new messages have accrued since the last summary.
    /// </summary>
    public int? SummarizedThroughSequence { get; set; }

    public ConversationStatus Status { get; set; } = ConversationStatus.Active;

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

    public void Rename(string title) => Title = title;

    public void Archive() => Status = ConversationStatus.Archived;

    public void UpdateSummary(string summary, int throughSequence)
    {
        RollingSummary = summary;
        SummarizedThroughSequence = throughSequence;
    }

    public bool BelongsToDoctor(string doctorId) =>
        string.Equals(DoctorId, doctorId, StringComparison.Ordinal);
}
