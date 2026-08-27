using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Patients.GetPatientHistory;

public record GetPatientHistoryQuery(
    int PatientId,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    bool IncludeTranscripts = false,
    bool IncludeStructuredData = true,
    int? Page = null,
    int? PageSize = null,
    string? Source = null) : IRequest<PatientHistoryDto>;

public class PatientHistoryDto
{
    public required GetPatientById.PatientDto Patient { get; set; }
    public List<ConsultationHistoryItemDto> Consultations { get; set; } = [];
    public List<PatientDoctorNoteDto> DoctorNotes { get; set; } = [];
    public int TotalConsultations { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; }
}

public class PatientDoctorNoteDto
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int? ConsultationId { get; set; }
    public required string Content { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? DateModified { get; set; }
}

public class ConsultationHistoryItemDto
{
    public int Id { get; set; }
    public DateTime ConsultationDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? DurationSeconds { get; set; }
    public bool HasAudio { get; set; }
    public bool HasDocument { get; set; }
    /// <summary>Audio, Pdf, or Unknown.</summary>
    public string Source { get; set; } = "Unknown";
    public string? TranscriptSnippet { get; set; }
    public string? StructuredSummary { get; set; }
}
