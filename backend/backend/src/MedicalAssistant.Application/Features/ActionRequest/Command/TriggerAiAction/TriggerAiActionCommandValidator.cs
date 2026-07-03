using FluentValidation;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;

namespace MedicalAssistant.Application.Features.ActionRequest.Command.TriggerAiAction;

public class TriggerAiActionCommandValidator : AbstractValidator<TriggerAiActionCommand>
{
    private readonly IActionRequestRepository _actionRequestRepository;
    private readonly IUserService _userService;

    public TriggerAiActionCommandValidator(IActionRequestRepository actionRequestRepository, IUserService userService)
    {
        _actionRequestRepository = actionRequestRepository;
        _userService = userService;

        RuleFor(a => a.ActionType).IsInEnum();
        RuleFor(a => a.CorrelationId).MaximumLength(64);
        RuleFor(a => a)
            .Must(a => a.PatientId.HasValue || a.ConsultationId.HasValue)
            .WithMessage("Either PatientId or ConsultationId must be provided.");
        RuleFor(a => a.CorrelationId)
            .MustAsync(CorrelationIdAvailable)
            .When(a => !string.IsNullOrWhiteSpace(a.CorrelationId))
            .WithMessage("Correlation ID already in use by another doctor.");
    }

    private async Task<bool> CorrelationIdAvailable(string? correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return true;

        var existing = await _actionRequestRepository.GetByCorrelationIdAsync(correlationId);
        if (existing == null) return true;

        var doctorId = await _userService.GetCurrentUserIdAsync();
        return string.Equals(existing.DoctorId, doctorId, StringComparison.Ordinal);
    }
}
