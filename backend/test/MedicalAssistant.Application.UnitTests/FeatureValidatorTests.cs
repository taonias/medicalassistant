using FluentValidation.TestHelper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Features.Chat.Command.SubmitChatQuery;
using MedicalAssistant.Application.Features.Chat.Queries.GetChatRequestStatus;
using MedicalAssistant.Application.Features.DoctorNotes.Command.CreateDoctorNote;
using MedicalAssistant.Application.Features.Consultation.Command.CreateConsultation;
using MedicalAssistant.Application.Features.Patient.Command.CreatePatient;
using MedicalAssistant.Application.Features.Auth;
using MedicalAssistant.Application.Models.Identity;
using MedicalAssistant.Domain;
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

public class SubmitChatQueryCommandValidatorTests
{
    [Fact]
    public async Task Should_require_message()
    {
        var validator = new SubmitChatQueryCommandValidator();
        var result = await validator.TestValidateAsync(new SubmitChatQueryCommand
        {
            Message = "",
            PatientId = 1
        });
        result.ShouldHaveValidationErrorFor(c => c.Message);
    }

    [Fact]
    public async Task Should_allow_general_chat_without_patient_or_consultation()
    {
        var validator = new SubmitChatQueryCommandValidator();
        var result = await validator.TestValidateAsync(new SubmitChatQueryCommand { Message = "Hello" });
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class GetChatRequestStatusQueryValidatorTests
{
    [Fact]
    public async Task Should_require_correlation_id()
    {
        var validator = new GetChatRequestStatusQueryValidator();
        var result = await validator.TestValidateAsync(new GetChatRequestStatusQuery(""));
        result.ShouldHaveValidationErrorFor(q => q.CorrelationId);
    }
}

public class CreateDoctorNoteCommandValidatorTests
{
    [Fact]
    public async Task Should_require_content()
    {
        var validator = new CreateDoctorNoteCommandValidator();
        var result = await validator.TestValidateAsync(new CreateDoctorNoteCommand
        {
            Content = "",
            PatientId = 1
        });
        result.ShouldHaveValidationErrorFor(c => c.Content);
    }
}

public class RegistrationRequestValidatorTests
{
    private readonly RegistrationRequestValidator _validator = new();

    private static RegistrationRequest ValidRequest() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane.doe@example.com",
        UserName = "jadoe",
        Password = "Password1"
    };

    [Fact]
    public async Task Should_have_error_when_first_name_empty()
    {
        var request = ValidRequest();
        request.FirstName = "";
        var result = await _validator.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(r => r.FirstName);
    }

    [Fact]
    public async Task Should_have_error_when_last_name_empty()
    {
        var request = ValidRequest();
        request.LastName = "";
        var result = await _validator.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(r => r.LastName);
    }

    [Fact]
    public async Task Should_have_error_when_email_invalid()
    {
        var request = ValidRequest();
        request.Email = "not-an-email";
        var result = await _validator.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(r => r.Email);
    }

    [Fact]
    public async Task Should_have_error_when_password_too_weak()
    {
        var request = ValidRequest();
        request.Password = "abc";
        var result = await _validator.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(r => r.Password);
    }

    [Fact]
    public async Task Should_not_have_error_when_request_is_valid()
    {
        var result = await _validator.TestValidateAsync(ValidRequest());
        result.ShouldNotHaveAnyValidationErrors();
    }
}
