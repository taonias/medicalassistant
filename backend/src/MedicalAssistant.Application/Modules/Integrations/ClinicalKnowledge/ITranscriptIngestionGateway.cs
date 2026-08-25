namespace MedicalAssistant.Application.Contracts.ClinicalKnowledge;

/// <summary>Submits a session transcript to Clinical Knowledge for ingestion (R27).</summary>
public interface ITranscriptIngestionGateway
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
    public string DocumentId => $"{DoctorId}#{PatientId}#{SessionId}#{SequenceNumber}";
}

public sealed record ClinicalKnowledgeIngestionAccepted(
    Guid IngestionId,
    bool Duplicate);
