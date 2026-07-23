namespace MedicalAssistant.Application.Contracts.Persistence;

public class DraftConsultationListItem
{
    public int PatientId { get; set; }
    public required string PatientFirstName { get; set; }
    public required string PatientLastName { get; set; }
    public int ConsultationId { get; set; }
    public DateTime ConsultationDate { get; set; }
    public int? DurationSeconds { get; set; }
    public bool HasAudio { get; set; }
}
