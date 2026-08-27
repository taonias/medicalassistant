using MedicalAssistant.Application.Models.Patients;
using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Patients.GetPatientsList;

public record GetPatientsListQuery : IRequest<IReadOnlyList<PatientListItem>>;
