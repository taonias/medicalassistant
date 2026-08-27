using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Domain;
using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.DoctorNotes.GetDoctorNotesByConsultation;

public record GetDoctorNotesByConsultationQuery(int ConsultationId) : IRequest<IReadOnlyList<DoctorNote>>;

public class GetDoctorNotesByConsultationQueryHandler : IRequestHandler<GetDoctorNotesByConsultationQuery, IReadOnlyList<DoctorNote>>
{
    private readonly IConsultationAccess _consultationRepository;
    private readonly IUserService _userService;
    private readonly IDoctorNoteRepository _doctorNoteRepository;

    public GetDoctorNotesByConsultationQueryHandler(
        IConsultationAccess consultationRepository,
        IUserService userService,
        IDoctorNoteRepository doctorNoteRepository)
    {
        _consultationRepository = consultationRepository;
        _userService = userService;
        _doctorNoteRepository = doctorNoteRepository;
    }

    public async Task<IReadOnlyList<DoctorNote>> Handle(GetDoctorNotesByConsultationQuery request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var consultation = await _consultationRepository.GetConsultationForDoctorAsync(request.ConsultationId, doctorId);
        if (consultation == null)
            throw new NotFoundException(nameof(Consultation), request.ConsultationId);

        return await _doctorNoteRepository.GetByConsultationIdForDoctorAsync(request.ConsultationId, doctorId);
    }
}
