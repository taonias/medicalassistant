using MedicalAssistant.Domain.Enums;

namespace MedicalAssistant.Domain;

public class ConsultationDeletionCleanup
{
    public long Id { get; set; }
    public int ConsultationId { get; set; }
    public Guid DeletionEventId { get; set; }
    public DateTime DeletedAtUtc { get; set; }
    public ConsultationCleanupStatus BlobCleanupStatus { get; set; } = ConsultationCleanupStatus.Pending;
    public ConsultationCleanupStatus ClinicalKnowledgeCleanupStatus { get; set; } = ConsultationCleanupStatus.Pending;
    public DateTime? BlobCleanupCompletedAtUtc { get; set; }
    public DateTime? ClinicalKnowledgeCleanupCompletedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastFailureCategory { get; set; }
    public string? LastFailureCode { get; set; }
}
