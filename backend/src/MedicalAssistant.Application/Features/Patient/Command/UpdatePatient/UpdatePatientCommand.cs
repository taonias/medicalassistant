using MediatR;

namespace MedicalAssistant.Application.Features.Patient.Command.UpdatePatient;

public class UpdatePatientCommand : IRequest<Queries.GetPatientById.PatientDto>
{
    public int Id { get; set; }
    public string? ExternalPatientId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}
