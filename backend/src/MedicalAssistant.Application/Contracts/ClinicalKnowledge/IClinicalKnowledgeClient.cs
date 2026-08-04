namespace MedicalAssistant.Application.Contracts.ClinicalKnowledge;

public interface IClinicalKnowledgeClient
{
    Task<ClinicalKnowledgeIngestionAccepted> SubmitSessionTranscriptAsync(
        ClinicalKnowledgeSessionTranscriptRequest request,
        CancellationToken cancellationToken = default);
}

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
}

public sealed record ClinicalKnowledgeIngestionAccepted(
    Guid IngestionId,
    bool Duplicate);
