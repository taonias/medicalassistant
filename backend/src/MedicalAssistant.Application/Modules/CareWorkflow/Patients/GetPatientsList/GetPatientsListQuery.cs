using MedicalAssistant.Application.Models.Patients;
using MediatR;

namespace MedicalAssistant.Application.Features.Patient.Queries.GetPatientsList;

public record GetPatientsListQuery : IRequest<IReadOnlyList<PatientListItem>>;
