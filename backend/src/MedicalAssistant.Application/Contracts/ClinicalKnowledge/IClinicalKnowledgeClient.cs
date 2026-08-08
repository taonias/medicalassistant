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
}

public sealed record ClinicalKnowledgeChatRequest(
    string PatientId,
    string DoctorId,
    string Question,
    int TopK = 5);

public sealed record ClinicalKnowledgeAnswer(
    string Text,
    bool Refused,
    bool RetrievalUsed,
    IReadOnlyList<ClinicalKnowledgeCitation> Citations);

public sealed record ClinicalKnowledgeCitation(
    string Label,
    string Quote,
    string DocumentType,
    string? SessionId);

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
