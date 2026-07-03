using MediatR;

namespace MedicalAssistant.Application.Features.Patient.Queries.GetPatientHistory;

public record GetPatientHistoryQuery(
    int PatientId,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    bool IncludeTranscripts = true,
    bool IncludeStructuredData = true) : IRequest<PatientHistoryDto>;

public class PatientHistoryDto
{
    public required Queries.GetPatientById.PatientDto Patient { get; set; }
    public List<ConsultationHistoryItemDto> Consultations { get; set; } = [];
}

public class ConsultationHistoryItemDto
{
    public int Id { get; set; }
    public DateTime ConsultationDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TranscriptSnippet { get; set; }
    public string? StructuredSummary { get; set; }
}
