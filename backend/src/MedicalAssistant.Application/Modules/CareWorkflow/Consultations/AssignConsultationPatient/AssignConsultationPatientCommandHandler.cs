using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;
using MediatR;

namespace MedicalAssistant.Application.Features.Consultation.Command.AssignConsultationPatient;

public class AssignConsultationPatientCommandHandler
    : IRequestHandler<AssignConsultationPatientCommand, ConsultationDto>
{
    private readonly IConsultationPatientAssignment _consultationRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public AssignConsultationPatientCommandHandler(
        IConsultationPatientAssignment consultationRepository,
        IPatientRepository patientRepository,
        IUserService userService,
        IMapper mapper)
    {
        _consultationRepository = consultationRepository;
        _patientRepository = patientRepository;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<ConsultationDto> Handle(
        AssignConsultationPatientCommand request,
        CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var consultation = await _consultationRepository.GetConsultationForDoctorAsync(
                request.ConsultationId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Consultation), request.ConsultationId);

        if (consultation.PatientId.HasValue)
            throw new BadRequestException("Consultation is already assigned to a patient.");

        _ = await _patientRepository.GetPatientForDoctorAsync(request.PatientId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Patient), request.PatientId);

        consultation.PatientId = request.PatientId;
        await _consultationRepository.UpdateAsync(consultation);

        var dto = _mapper.Map<ConsultationDto>(consultation);
        dto.Status = consultation.Status.ToString();
        return dto;
    }
}
