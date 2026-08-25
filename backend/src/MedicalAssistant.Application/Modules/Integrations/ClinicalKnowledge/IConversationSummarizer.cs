namespace MedicalAssistant.Application.Contracts.ClinicalKnowledge;

/// <summary>Folds older conversation turns into an updated rolling summary (R27).</summary>
public interface IConversationSummarizer
{
    Task<string> SummarizeConversationAsync(
        ClinicalKnowledgeSummarizeRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ClinicalKnowledgeSummarizeRequest(
    string PatientId,
    string? PriorSummary,
    IReadOnlyList<ClinicalKnowledgeConversationTurn> NewTurns);
