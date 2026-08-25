using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MediatR;

namespace MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationsByPatient;

public class GetConsultationsByPatientQueryHandler : IRequestHandler<GetConsultationsByPatientQuery, List<ConsultationSummaryDto>>
{
    private readonly IConsultationListing _consultationRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IUserService _userService;

    public GetConsultationsByPatientQueryHandler(
        IConsultationListing consultationRepository,
        IPatientRepository patientRepository,
        IUserService userService)
    {
        _consultationRepository = consultationRepository;
        _patientRepository = patientRepository;
        _userService = userService;
    }

    public async Task<List<ConsultationSummaryDto>> Handle(GetConsultationsByPatientQuery request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        _ = await _patientRepository.GetPatientForDoctorAsync(request.PatientId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Patient), request.PatientId);

        var consultations = await _consultationRepository.GetConsultationsByPatientForDoctorAsync(request.PatientId, doctorId);

        return consultations
            .OrderByDescending(c => c.ConsultationDate)
            .Select(c => new ConsultationSummaryDto
            {
                Id = c.Id,
                ConsultationDate = c.ConsultationDate,
                Status = c.Status.ToString(),
                HasAudio = !string.IsNullOrEmpty(c.AudioBlobUri),
                DurationSeconds = c.DurationSeconds,
            })
            .ToList();
    }
}
