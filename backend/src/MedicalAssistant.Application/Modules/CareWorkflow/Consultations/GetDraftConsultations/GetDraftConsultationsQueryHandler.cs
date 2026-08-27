using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetConsultationsByPatient;
using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetDraftConsultations;

public class GetDraftConsultationsQueryHandler
    : IRequestHandler<GetDraftConsultationsQuery, List<DraftConsultationGroupDto>>
{
    private readonly IConsultationListing _consultationRepository;
    private readonly IUserService _userService;

    public GetDraftConsultationsQueryHandler(
        IConsultationListing consultationRepository,
        IUserService userService)
    {
        _consultationRepository = consultationRepository;
        _userService = userService;
    }

    public async Task<List<DraftConsultationGroupDto>> Handle(
        GetDraftConsultationsQuery request,
        CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var drafts = await _consultationRepository.GetDraftConsultationsForDoctorAsync(doctorId);

        return drafts
            .GroupBy(item => item.PatientId)
            .Select(group =>
            {
                var first = group.First();
                return new DraftConsultationGroupDto
                {
                    PatientId = group.Key,
                    FirstName = first.PatientFirstName,
                    LastName = first.PatientLastName,
                    Consultations = group
                        .                        Select(item => new ConsultationSummaryDto
                        {
                            Id = item.ConsultationId,
                            ConsultationDate = item.ConsultationDate,
                            Status = "Draft",
                            HasAudio = item.HasAudio,
                            DurationSeconds = item.DurationSeconds,
                        })
                        .OrderByDescending(c => c.ConsultationDate)
                        .ToList(),
                };
            })
            .OrderBy(group => group.LastName)
            .ThenBy(group => group.FirstName)
            .ToList();
    }
}
