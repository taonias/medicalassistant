using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Modules.CareWorkflow.Patients.GetPatientById;
using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Patients.CreatePatient;

public class CreatePatientCommandHandler : IRequestHandler<CreatePatientCommand, PatientDto>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public CreatePatientCommandHandler(
        IPatientRepository patientRepository,
        IUserService userService,
        IMapper mapper)
    {
        _patientRepository = patientRepository;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<PatientDto> Handle(CreatePatientCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var patient = _mapper.Map<Domain.Patient>(request);
        patient.AssignedDoctorId = doctorId;

        await _patientRepository.CreateAsync(patient);
        return _mapper.Map<PatientDto>(patient);
    }
}
