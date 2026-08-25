namespace MedicalAssistant.Application.Contracts.ClinicalKnowledge;

/// <summary>Removes a document's clinical-knowledge record on consultation deletion (R27).</summary>
public interface IClinicalKnowledgeDeletionGateway
{
    Task<ClinicalKnowledgeUnIngestResult> UnIngestDocumentAsync(
        string documentId,
        string removedBy,
        CancellationToken cancellationToken = default);
}

public sealed record ClinicalKnowledgeUnIngestResult(
    string DocumentId,
    ClinicalKnowledgeUnIngestStatus Status);

public enum ClinicalKnowledgeUnIngestStatus
{
    Removed = 0,
    AlreadyMissing = 1
}
