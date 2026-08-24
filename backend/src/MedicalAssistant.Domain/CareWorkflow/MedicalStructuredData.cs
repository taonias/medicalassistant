using MedicalAssistant.Domain.Common;

namespace MedicalAssistant.Domain;

public class MedicalStructuredData : BaseEntity
{
    public int ConsultationId { get; set; }
    public int? TranscriptId { get; set; }
    public required string SchemaVersion { get; set; }
    public required string StructuredPayload { get; set; }
    public bool Approved { get; set; } = false;
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
}
