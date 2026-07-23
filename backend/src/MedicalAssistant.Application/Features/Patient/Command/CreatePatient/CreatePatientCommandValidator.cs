using FluentValidation;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;

namespace MedicalAssistant.Application.Features.Patient.Command.CreatePatient;

public class CreatePatientCommandValidator : AbstractValidator<CreatePatientCommand>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IUserService _userService;

    public CreatePatientCommandValidator(IPatientRepository patientRepository, IUserService userService)
    {
        _patientRepository = patientRepository;
        _userService = userService;

        RuleFor(p => p.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(p => p.LastName).NotEmpty().MaximumLength(100);
        RuleFor(p => p.ExternalPatientId)
            .MaximumLength(100)
            .MustAsync(ExternalPatientIdUnique)
            .When(p => !string.IsNullOrWhiteSpace(p.ExternalPatientId))
            .WithMessage("External patient ID must be unique for this doctor.");
    }

    private async Task<bool> ExternalPatientIdUnique(string? externalPatientId, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync();
        if (string.IsNullOrEmpty(doctorId) || string.IsNullOrWhiteSpace(externalPatientId))
            return true;

        return await _patientRepository.IsExternalPatientIdUniqueAsync(externalPatientId, doctorId);
    }
}
