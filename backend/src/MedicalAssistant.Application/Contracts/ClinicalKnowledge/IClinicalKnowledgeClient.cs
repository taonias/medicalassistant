namespace MedicalAssistant.Application.Contracts.ClinicalKnowledge;

public interface IClinicalKnowledgeClient
{
    Task<ClinicalKnowledgeIngestionAccepted> SubmitSessionTranscriptAsync(
        ClinicalKnowledgeSessionTranscriptRequest request,
        CancellationToken cancellationToken = default);

    Task<ClinicalKnowledgeUnIngestResult> UnIngestDocumentAsync(
        string documentId,
        string removedBy,
        CancellationToken cancellationToken = default);

    Task<ClinicalKnowledgeAnswer> GetGroundedAnswerAsync(
        ClinicalKnowledgeChatRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Folds older conversation turns into an updated rolling summary.</summary>
    Task<string> SummarizeConversationAsync(
        ClinicalKnowledgeSummarizeRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ClinicalKnowledgeSummarizeRequest(
    string PatientId,
    string? PriorSummary,
    IReadOnlyList<ClinicalKnowledgeConversationTurn> NewTurns);

public sealed record ClinicalKnowledgeChatRequest(
    string PatientId,
    string DoctorId,
    string Question,
    int TopK = 5,
    IReadOnlyList<ClinicalKnowledgeConversationTurn>? RecentTurns = null,
    string? PriorSummary = null,
    Guid? AskId = null);

/// <summary>One prior turn replayed to the AI for phrasing/refinement only (never evidence).</summary>
public sealed record ClinicalKnowledgeConversationTurn(string Role, string Text);

public sealed record ClinicalKnowledgeAnswer(
    string Text,
    bool Refused,
    bool RetrievalUsed,
    string Language,
    IReadOnlyList<ClinicalKnowledgeCitation> Citations);

/// <summary>
/// One cited Evidence Item, carried through structurally (not flattened to a
/// string) so the backend can persist and richly render the answer's grounding.
/// Mirrors the AI service's ChatCitation field-for-field.
/// </summary>
public sealed record ClinicalKnowledgeCitation(
    string Label,
    Guid ChunkId,
    string DocumentId,
    string DocumentType,
    string? SessionId,
    DateTimeOffset? DocumentDate,
    string? SourceRef,
    string Quote,
    double Score);

public sealed record ClinicalKnowledgeSessionTranscriptRequest(
    string DoctorId,
    string PatientId,
    string SessionId,
    int SequenceNumber,
    DateTimeOffset? SessionDate,
    string? Language,
    string Transcript)
{
    public string DocumentType => "SessionTranscript";
    public string DocumentId => $"{DoctorId}#{PatientId}#{SessionId}#{SequenceNumber}";
}

public sealed record ClinicalKnowledgeIngestionAccepted(
    Guid IngestionId,
    bool Duplicate);

public sealed record ClinicalKnowledgeUnIngestResult(
    string DocumentId,
    ClinicalKnowledgeUnIngestStatus Status);

public enum ClinicalKnowledgeUnIngestStatus
{
    Removed = 0,
    AlreadyMissing = 1
}
