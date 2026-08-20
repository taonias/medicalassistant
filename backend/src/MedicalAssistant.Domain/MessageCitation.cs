using MedicalAssistant.Domain.Common;

namespace MedicalAssistant.Domain;

/// <summary>
/// One cited Evidence Item behind an assistant answer, persisted structurally
/// (not flattened to a string) so the answer's grounding can be re-rendered from
/// history. Mirrors the AI service's ChatCitation.
/// </summary>
public class MessageCitation : BaseEntity
{
    public int ChatMessageId { get; set; }

    /// <summary>The label the answer cites this evidence by — E1, E2, … in retrieval order.</summary>
    public required string Label { get; set; }

    /// <summary>The chunk this evidence came from.</summary>
    public Guid ChunkId { get; set; }

    /// <summary>The source Document.</summary>
    public required string DocumentId { get; set; }

    /// <summary>The source document's type.</summary>
    public required string DocumentType { get; set; }

    /// <summary>Session link, for session-scoped documents.</summary>
    public string? SessionId { get; set; }

    /// <summary>Clinical date of the source document.</summary>
    public DateTimeOffset? DocumentDate { get; set; }

    /// <summary>Type-specific provenance as JSON (e.g. a transcript line range).</summary>
    public string? SourceRef { get; set; }

    /// <summary>The verbatim chunk text, bounded in length — the reliable citation anchor.</summary>
    public required string Quote { get; set; }

    /// <summary>Cosine similarity to the question — higher is closer.</summary>
    public double Score { get; set; }
}
