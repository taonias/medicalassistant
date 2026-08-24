using MedicalAssistant.Domain.Common;

namespace MedicalAssistant.Domain;

public class DoctorNote : BaseEntity
{
    public required string DoctorId { get; set; }
    public int PatientId { get; set; }
    public int? ConsultationId { get; set; }

    // Free-form clinician note (stored as text, indexed by AI module for RAG).
    public required string Content { get; set; }
}
