using MedicalAssistant.Domain.Common;
using MedicalAssistant.Domain.Enums;

namespace MedicalAssistant.Domain;

public class Consultation : BaseEntity
{
    public int? PatientId { get; set; }
    public required string DoctorId { get; set; }
    public DateTime ConsultationDate { get; set; }
    public ConsultationStatus Status { get; set; } = ConsultationStatus.Draft;
    public string? AudioBlobUri { get; set; }
    public string? AudioContentType { get; set; }
    public string? DocumentBlobUri { get; set; }
    public string? DocumentContentType { get; set; }
    public string? DocumentFileName { get; set; }
    public int? DurationSeconds { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? FailureReason { get; set; }
    public ConsultationFileKind SourceFileKind { get; set; } = ConsultationFileKind.Unknown;
    public string? SourceObjectReference { get; set; }
    public string? SourceObjectETag { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
    public string? DeletionReasonCode { get; set; }

    public void MarkAudioUploaded(string blobUri, string contentType, int? durationSeconds)
    {
        AudioBlobUri = blobUri;
        AudioContentType = contentType;
        DurationSeconds = durationSeconds;
        SourceFileKind = ConsultationFileKind.Audio;
        SourceObjectReference = blobUri;
        Status = ConsultationStatus.AudioUploaded;
    }

    public void MarkDocumentUploaded(string blobUri, string contentType, string fileName)
    {
        DocumentBlobUri = blobUri;
        DocumentContentType = contentType;
        DocumentFileName = fileName;
        SourceFileKind = ConsultationFileKind.Document;
        SourceObjectReference = blobUri;
        Status = ConsultationStatus.DocumentUploaded;
    }

    public void MarkTranscribing()
    {
        Status = ConsultationStatus.Transcribing;
    }

    public void MarkTranscribed()
    {
        Status = ConsultationStatus.Transcribed;
    }

    public void MarkStructuredDataPending()
    {
        Status = ConsultationStatus.StructuredDataPending;
    }

    public void MarkCompleted()
    {
        Status = ConsultationStatus.Completed;
    }

    public void MarkFailed(string reason)
    {
        Status = ConsultationStatus.Failed;
        FailureReason = reason;
    }

    public void MarkDeleted(string deletedBy, string? reasonCode)
    {
        Status = ConsultationStatus.Deleted;
        DeletedAtUtc = DateTime.UtcNow;
        DeletedBy = deletedBy;
        DeletionReasonCode = string.IsNullOrWhiteSpace(reasonCode)
            ? "doctor-delete"
            : reasonCode.Trim();
    }
}
