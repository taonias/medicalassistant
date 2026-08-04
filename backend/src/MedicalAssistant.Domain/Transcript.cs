using MedicalAssistant.Domain.Common;
using MedicalAssistant.Domain.Enums;

namespace MedicalAssistant.Domain;

public class Transcript : BaseEntity
{
    public int ConsultationId { get; set; }
    public TranscriptStatus Status { get; set; } = TranscriptStatus.Pending;
    /// <summary>Persisted as column <c>Transcript</c>.</summary>
    public string? TranscriptText { get; set; }
    public string? ExternalJobId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? FailureReason { get; set; }
    public int Revision { get; set; } = 1;
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    public void MarkProcessing(string externalJobId)
    {
        Status = TranscriptStatus.Processing;
        ExternalJobId = externalJobId;
    }

    public void MarkCompleted(string transcript)
    {
        var hasExistingResult = Status == TranscriptStatus.Completed || !string.IsNullOrWhiteSpace(TranscriptText);
        Status = TranscriptStatus.Completed;
        TranscriptText = transcript;
        ProcessedAt = DateTime.UtcNow;
        Revision = hasExistingResult
            ? Math.Max(Revision, 1) + 1
            : Math.Max(Revision, 1);
        ConcurrencyToken = Guid.NewGuid();
    }

    public void MarkFailed(string reason)
    {
        Status = TranscriptStatus.Failed;
        FailureReason = reason;
        ProcessedAt = DateTime.UtcNow;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void UpdateText(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            throw new ArgumentException("Transcript text is required.", nameof(transcript));

        TranscriptText = transcript.Trim();
        Status = TranscriptStatus.Completed;
        FailureReason = null;
        ProcessedAt ??= DateTime.UtcNow;
        Revision++;
        ConcurrencyToken = Guid.NewGuid();
    }
}
