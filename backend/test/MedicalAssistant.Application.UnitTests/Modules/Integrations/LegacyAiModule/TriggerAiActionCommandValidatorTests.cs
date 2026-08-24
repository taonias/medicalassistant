using FluentValidation.TestHelper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Features.ActionRequest.Command.TriggerAiAction;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using Moq;

namespace MedicalAssistant.Application.UnitTests.Features;

public class TriggerAiActionCommandValidatorTests
{
    private readonly Mock<IActionRequestRepository> _actionRepository = new();
    private readonly Mock<IUserService> _userService = new();

    public TriggerAiActionCommandValidatorTests()
    {
        _userService.Setup(s => s.GetCurrentUserIdAsync()).ReturnsAsync("doctor-1");
    }

    [Fact]
    public async Task Should_allow_same_correlation_for_same_doctor()
    {
        _actionRepository
            .Setup(r => r.GetByCorrelationIdAsync("corr-1"))
            .ReturnsAsync(new ActionRequest
            {
                CorrelationId = "corr-1",
                DoctorId = "doctor-1",
                ActionType = ActionType.SummarizeConsultation
            });

        var validator = new TriggerAiActionCommandValidator(_actionRepository.Object, _userService.Object);
        var result = await validator.TestValidateAsync(new TriggerAiActionCommand
        {
            ActionType = ActionType.SummarizeConsultation,
            PatientId = 1,
            CorrelationId = "corr-1"
        });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Should_reject_correlation_owned_by_other_doctor()
    {
        _actionRepository
            .Setup(r => r.GetByCorrelationIdAsync("corr-2"))
            .ReturnsAsync(new ActionRequest
            {
                CorrelationId = "corr-2",
                DoctorId = "other-doctor",
                ActionType = ActionType.SummarizeConsultation
            });

        var validator = new TriggerAiActionCommandValidator(_actionRepository.Object, _userService.Object);
        var result = await validator.TestValidateAsync(new TriggerAiActionCommand
        {
            ActionType = ActionType.SummarizeConsultation,
            PatientId = 1,
            CorrelationId = "corr-2"
        });
        result.ShouldHaveValidationErrorFor(c => c.CorrelationId);
    }
}
