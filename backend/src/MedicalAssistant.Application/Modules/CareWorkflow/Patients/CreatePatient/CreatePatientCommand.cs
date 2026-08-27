using MediatR;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Modules.CareWorkflow.Patients.GetPatientById;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Patients.CreatePatient;

public class CreatePatientCommand : IRequest<PatientDto>, IAuditableRequest<PatientDto>
{
    public string? ExternalPatientId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    public AuditEntry ToAuditEntry(PatientDto response) =>
        new("CreatePatient", "Patient", response.Id.ToString());
}
