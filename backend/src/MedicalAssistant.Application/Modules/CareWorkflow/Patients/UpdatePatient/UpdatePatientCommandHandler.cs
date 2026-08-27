using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Modules.CareWorkflow.Patients.GetPatientById;
using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Patients.UpdatePatient;

public class UpdatePatientCommandHandler : IRequestHandler<UpdatePatientCommand, PatientDto>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public UpdatePatientCommandHandler(
        IPatientRepository patientRepository,
        IUserService userService,
        IMapper mapper)
    {
        _patientRepository = patientRepository;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<PatientDto> Handle(UpdatePatientCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var patient = await _patientRepository.GetPatientForDoctorAsync(request.Id, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Patient), request.Id);

        patient.ExternalPatientId = request.ExternalPatientId;
        patient.FirstName = request.FirstName;
        patient.LastName = request.LastName;
        patient.DateOfBirth = request.DateOfBirth;

        await _patientRepository.UpdateAsync(patient);
        return _mapper.Map<PatientDto>(patient);
    }
}
