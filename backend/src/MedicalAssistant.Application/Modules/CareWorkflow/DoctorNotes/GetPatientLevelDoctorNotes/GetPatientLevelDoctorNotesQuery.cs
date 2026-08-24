using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Domain;
using MediatR;

namespace MedicalAssistant.Application.Features.DoctorNotes.Query.GetPatientLevelDoctorNotes;

public record GetPatientLevelDoctorNotesQuery(int PatientId) : IRequest<IReadOnlyList<DoctorNote>>;

public class GetPatientLevelDoctorNotesQueryHandler
    : IRequestHandler<GetPatientLevelDoctorNotesQuery, IReadOnlyList<DoctorNote>>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IUserService _userService;
    private readonly IDoctorNoteRepository _doctorNoteRepository;

    public GetPatientLevelDoctorNotesQueryHandler(
        IPatientRepository patientRepository,
        IUserService userService,
        IDoctorNoteRepository doctorNoteRepository)
    {
        _patientRepository = patientRepository;
        _userService = userService;
        _doctorNoteRepository = doctorNoteRepository;
    }

    public async Task<IReadOnlyList<DoctorNote>> Handle(
        GetPatientLevelDoctorNotesQuery request,
        CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var patient = await _patientRepository.GetPatientForDoctorAsync(request.PatientId, doctorId);
        if (patient == null)
            throw new NotFoundException(nameof(Patient), request.PatientId);

        return await _doctorNoteRepository.GetPatientLevelNotesForDoctorAsync(request.PatientId, doctorId);
    }
}
