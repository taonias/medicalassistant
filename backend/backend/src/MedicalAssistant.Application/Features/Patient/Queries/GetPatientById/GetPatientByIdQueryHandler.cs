using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MediatR;

namespace MedicalAssistant.Application.Features.Patient.Queries.GetPatientById;

public class GetPatientByIdQueryHandler : IRequestHandler<GetPatientByIdQuery, PatientDto>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public GetPatientByIdQueryHandler(
        IPatientRepository patientRepository,
        IUserService userService,
        IMapper mapper)
    {
        _patientRepository = patientRepository;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<PatientDto> Handle(GetPatientByIdQuery request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var patient = await _patientRepository.GetPatientForDoctorAsync(request.Id, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Patient), request.Id);

        return _mapper.Map<PatientDto>(patient);
    }
}
