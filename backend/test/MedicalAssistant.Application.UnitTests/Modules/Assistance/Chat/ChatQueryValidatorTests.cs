using FluentValidation.TestHelper;
using MedicalAssistant.Application.Modules.Assistance.Chat.ChatQuery;

namespace MedicalAssistant.Application.UnitTests.Features;

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
