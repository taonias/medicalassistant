using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Models.Patients;
using MediatR;

namespace MedicalAssistant.Application.Features.Patient.Queries.GetPatientsList;

public class GetPatientsListQueryHandler : IRequestHandler<GetPatientsListQuery, IReadOnlyList<PatientListItem>>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IUserService _userService;

    public GetPatientsListQueryHandler(IPatientRepository patientRepository, IUserService userService)
    {
        _patientRepository = patientRepository;
        _userService = userService;
    }

    public async Task<IReadOnlyList<PatientListItem>> Handle(
        GetPatientsListQuery request,
        CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        return await _patientRepository.GetPatientListForDoctorAsync(doctorId);
    }
}
