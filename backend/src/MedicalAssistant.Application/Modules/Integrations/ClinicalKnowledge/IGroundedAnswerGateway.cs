namespace MedicalAssistant.Application.Contracts.ClinicalKnowledge;

/// <summary>Asks Clinical Knowledge for a grounded, evidence-cited answer (R27).</summary>
public interface IGroundedAnswerGateway
{
    Task<ClinicalKnowledgeAnswer> GetGroundedAnswerAsync(
        ClinicalKnowledgeChatRequest request,
        CancellationToken cancellationToken = default);
}

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
