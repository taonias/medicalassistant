using MedicalAssistant.Domain.Common;

namespace MedicalAssistant.Domain;

public class Patient : BaseEntity
{
    public string? ExternalPatientId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public required string AssignedDoctorId { get; set; }

    public bool BelongsToDoctor(string doctorId) =>
        string.Equals(AssignedDoctorId, doctorId, StringComparison.Ordinal);
}
