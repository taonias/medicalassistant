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

    public void MarkProcessing(string externalJobId)
    {
        Status = TranscriptStatus.Processing;
        ExternalJobId = externalJobId;
    }

    public void MarkCompleted(string transcript)
    {
        Status = TranscriptStatus.Completed;
        TranscriptText = transcript;
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        Status = TranscriptStatus.Failed;
        FailureReason = reason;
    }

    public void UpdateText(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            throw new ArgumentException("Transcript text is required.", nameof(transcript));

        TranscriptText = transcript.Trim();
        Status = TranscriptStatus.Completed;
        FailureReason = null;
        ProcessedAt ??= DateTime.UtcNow;
    }
}
