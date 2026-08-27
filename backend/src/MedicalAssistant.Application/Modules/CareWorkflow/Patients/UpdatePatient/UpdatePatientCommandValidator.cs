using FluentValidation;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Patients.UpdatePatient;

public class UpdatePatientCommandValidator : AbstractValidator<UpdatePatientCommand>
{
    private readonly IPatientRepository _patientRepository;
    private readonly IUserService _userService;

    public UpdatePatientCommandValidator(IPatientRepository patientRepository, IUserService userService)
    {
        _patientRepository = patientRepository;
        _userService = userService;

        RuleFor(p => p.Id).GreaterThan(0);
        RuleFor(p => p.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(p => p.LastName).NotEmpty().MaximumLength(100);
        RuleFor(p => p.ExternalPatientId)
            .MaximumLength(100)
            .MustAsync(ExternalPatientIdUnique)
            .When(p => !string.IsNullOrWhiteSpace(p.ExternalPatientId))
            .WithMessage("External patient ID must be unique for this doctor.");
    }

    private async Task<bool> ExternalPatientIdUnique(UpdatePatientCommand command, string? externalPatientId, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync();
        if (string.IsNullOrEmpty(doctorId) || string.IsNullOrWhiteSpace(externalPatientId))
            return true;

        return await _patientRepository.IsExternalPatientIdUniqueAsync(externalPatientId, doctorId, command.Id);
    }
}
