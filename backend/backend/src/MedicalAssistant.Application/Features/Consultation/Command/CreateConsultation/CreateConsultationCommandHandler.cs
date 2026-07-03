using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;
using MedicalAssistant.Domain.Enums;
using MediatR;

namespace MedicalAssistant.Application.Features.Consultation.Command.CreateConsultation;

public class CreateConsultationCommandHandler : IRequestHandler<CreateConsultationCommand, ConsultationDto>
{
    private readonly IConsultationRepository _consultationRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public CreateConsultationCommandHandler(
        IConsultationRepository consultationRepository,
        IPatientRepository patientRepository,
        IUserService userService,
        IMapper mapper)
    {
        _consultationRepository = consultationRepository;
        _patientRepository = patientRepository;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<ConsultationDto> Handle(CreateConsultationCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _consultationRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, doctorId);
            if (existing != null)
                return _mapper.Map<ConsultationDto>(existing);
        }

        if (request.PatientId.HasValue)
        {
            _ = await _patientRepository.GetPatientForDoctorAsync(request.PatientId.Value, doctorId)
                ?? throw new NotFoundException(nameof(Domain.Patient), request.PatientId.Value);
        }

        var consultation = new Domain.Consultation
        {
            PatientId = request.PatientId,
            DoctorId = doctorId,
            ConsultationDate = request.ConsultationDate,
            Status = ConsultationStatus.Draft,
            IdempotencyKey = request.IdempotencyKey
        };

        await _consultationRepository.CreateAsync(consultation);
        return _mapper.Map<ConsultationDto>(consultation);
    }
}
