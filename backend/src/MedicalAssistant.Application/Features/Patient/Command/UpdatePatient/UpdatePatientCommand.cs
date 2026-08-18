using MediatR;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Features.Patient.Queries.GetPatientById;

namespace MedicalAssistant.Application.Features.Patient.Command.UpdatePatient;

public class UpdatePatientCommand : IRequest<PatientDto>, IAuditableRequest<PatientDto>
{
    public int Id { get; set; }
    public string? ExternalPatientId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    public AuditEntry ToAuditEntry(PatientDto response) =>
        new("UpdatePatient", "Patient", Id.ToString());
}
