using FluentValidation.TestHelper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Features.Patient.Command.CreatePatient;
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
