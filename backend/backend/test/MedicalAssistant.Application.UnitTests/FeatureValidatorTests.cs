using FluentValidation.TestHelper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Features.Chat.Queries.ChatQuery;
using MedicalAssistant.Application.Features.Consultation.Command.CreateConsultation;
using MedicalAssistant.Application.Features.Patient.Command.CreatePatient;
using MedicalAssistant.Application.Features.ActionRequest.Command.TriggerAiAction;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using Moq;

namespace MedicalAssistant.Application.UnitTests.Features;

public class CreatePatientCommandValidatorTests
{
    private readonly Mock<IPatientRepository> _patientRepository = new();
    private readonly Mock<IUserService> _userService = new();

    public CreatePatientCommandValidatorTests()
    {
        _userService.Setup(s => s.GetCurrentUserIdAsync()).ReturnsAsync("doctor-1");
    }

    [Fact]
    public async Task Should_have_error_when_first_name_empty()
    {
        var validator = new CreatePatientCommandValidator(_patientRepository.Object, _userService.Object);
        var result = await validator.TestValidateAsync(new CreatePatientCommand
        {
            FirstName = "",
            LastName = "Doe"
        });
        result.ShouldHaveValidationErrorFor(c => c.FirstName);
    }

    [Fact]
    public async Task Should_have_error_when_external_id_not_unique()
    {
        _patientRepository
            .Setup(r => r.IsExternalPatientIdUniqueAsync("EXT-1", "doctor-1", null))
            .ReturnsAsync(false);

        var validator = new CreatePatientCommandValidator(_patientRepository.Object, _userService.Object);
        var result = await validator.TestValidateAsync(new CreatePatientCommand
        {
            FirstName = "Jane",
            LastName = "Doe",
            ExternalPatientId = "EXT-1"
        });
        result.ShouldHaveValidationErrorFor(c => c.ExternalPatientId);
    }
}

public class CreateConsultationCommandValidatorTests
{
    private readonly Mock<IPatientRepository> _patientRepository = new();
    private readonly Mock<IUserService> _userService = new();

    public CreateConsultationCommandValidatorTests()
    {
        _userService.Setup(s => s.GetCurrentUserIdAsync()).ReturnsAsync("doctor-1");
    }

    [Fact]
    public async Task Should_allow_consultation_without_patient()
    {
        var validator = new CreateConsultationCommandValidator(_patientRepository.Object, _userService.Object);
        var result = await validator.TestValidateAsync(new CreateConsultationCommand
        {
            ConsultationDate = DateTime.UtcNow
        });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Should_have_error_when_patient_not_assigned_to_doctor()
    {
        _patientRepository
            .Setup(r => r.GetPatientForDoctorAsync(99, "doctor-1"))
            .ReturnsAsync((Patient?)null);

        var validator = new CreateConsultationCommandValidator(_patientRepository.Object, _userService.Object);
        var result = await validator.TestValidateAsync(new CreateConsultationCommand
        {
            PatientId = 99,
            ConsultationDate = DateTime.UtcNow
        });
        result.ShouldHaveValidationErrorFor(c => c.PatientId!.Value);
    }
}

public class ChatQueryValidatorTests
{
    [Fact]
    public async Task Should_require_message()
    {
        var validator = new ChatQueryValidator();
        var result = await validator.TestValidateAsync(new ChatQuery
        {
            Message = "",
            PatientId = 1
        });
        result.ShouldHaveValidationErrorFor(c => c.Message);
    }

    [Fact]
    public async Task Should_allow_general_chat_without_patient_or_consultation()
    {
        var validator = new ChatQueryValidator();
        var result = await validator.TestValidateAsync(new ChatQuery { Message = "Hello" });
        result.ShouldNotHaveAnyValidationErrors();
    }
}

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
