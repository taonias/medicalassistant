using FluentValidation;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;

namespace MedicalAssistant.Application.Features.Consultation.Command.CreateConsultation;

public class CreateConsultationCommandValidator : AbstractValidator<CreateConsultationCommand>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IUserService _userService;

    public CreateConsultationCommandValidator(
        IPatientRepository patientRepository,
        IUserService userService)
    {
        _patientRepository = patientRepository;
        _userService = userService;

        RuleFor(c => c.ConsultationDate).NotEmpty();
        RuleFor(c => c.IdempotencyKey).MaximumLength(128);
        RuleFor(c => c.PatientId)
            .Must(id => !id.HasValue || id.Value > 0)
            .WithMessage("PatientId must be greater than zero when provided.");
        When(c => c.PatientId.HasValue && c.PatientId.Value > 0, () =>
        {
            RuleFor(c => c.PatientId!.Value).MustAsync(PatientBelongsToDoctor)
                .WithMessage("Patient not found or not assigned to the current doctor.");
        });
    }

    private async Task<bool> PatientBelongsToDoctor(int patientId, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync();
        if (string.IsNullOrEmpty(doctorId)) return false;
        var patient = await _patientRepository.GetPatientForDoctorAsync(patientId, doctorId);
        return patient != null;
    }
}
