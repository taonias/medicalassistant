using FluentValidation.TestHelper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Modules.CareWorkflow.Consultations.CreateConsultation;
using MedicalAssistant.Domain;
using Moq;

namespace MedicalAssistant.Application.UnitTests.Features;

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
