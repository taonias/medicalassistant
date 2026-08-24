namespace MedicalAssistant.Application.Models.Patients;

public class PatientListItem
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public DateTime? DateCreated { get; set; }
    public DateTime? LastConsultationDate { get; set; }
    public int ConsultationCount { get; set; }
}
