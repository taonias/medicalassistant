using MediatR;

namespace MedicalAssistant.Application.Features.Patient.Command.CreatePatient;

public class CreatePatientCommand : IRequest<Queries.GetPatientById.PatientDto>
{
    public string? ExternalPatientId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}
