using MedicalAssistant.Domain.Common;
using MedicalAssistant.Domain.Enums;

namespace MedicalAssistant.Domain;

public class Transcript : BaseEntity
{
    public int ConsultationId { get; set; }
    public TranscriptStatus Status { get; set; } = TranscriptStatus.Pending;
    public string? RawText { get; set; }
    public string? TranscriptBlobUri { get; set; }
    public string? ExternalJobId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? FailureReason { get; set; }

    public void MarkProcessing(string externalJobId)
    {
        Status = TranscriptStatus.Processing;
        ExternalJobId = externalJobId;
    }

    public void MarkCompleted(string rawText, string? blobUri = null)
    {
        Status = TranscriptStatus.Completed;
        RawText = rawText;
        TranscriptBlobUri = blobUri;
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        Status = TranscriptStatus.Failed;
        FailureReason = reason;
    }
}
