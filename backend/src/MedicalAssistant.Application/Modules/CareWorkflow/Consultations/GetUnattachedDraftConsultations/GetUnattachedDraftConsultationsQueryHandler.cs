using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetConsultationsByPatient;
using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetUnattachedDraftConsultations;

public class GetUnattachedDraftConsultationsQueryHandler
    : IRequestHandler<GetUnattachedDraftConsultationsQuery, List<ConsultationSummaryDto>>
{
    private readonly IConsultationListing _consultationRepository;
    private readonly IUserService _userService;

    public GetUnattachedDraftConsultationsQueryHandler(
        IConsultationListing consultationRepository,
        IUserService userService)
    {
        _consultationRepository = consultationRepository;
        _userService = userService;
    }

    public async Task<List<ConsultationSummaryDto>> Handle(
        GetUnattachedDraftConsultationsQuery request,
        CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var items = await _consultationRepository.GetUnattachedDraftConsultationsForDoctorAsync(doctorId);

        return items
            .Select(c => new ConsultationSummaryDto
            {
                Id = c.Id,
                ConsultationDate = c.ConsultationDate,
                Status = c.Status.ToString(),
                DurationSeconds = c.DurationSeconds,
                HasAudio = !string.IsNullOrEmpty(c.AudioBlobUri),
                HasDocument = !string.IsNullOrEmpty(c.DocumentBlobUri),
            })
            .ToList();
    }
}
