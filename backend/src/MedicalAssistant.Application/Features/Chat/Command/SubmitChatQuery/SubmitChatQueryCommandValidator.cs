using FluentValidation;

namespace MedicalAssistant.Application.Features.Chat.Command.SubmitChatQuery;

public class SubmitChatQueryCommandValidator : AbstractValidator<SubmitChatQueryCommand>
{
    public SubmitChatQueryCommandValidator()
    {
        RuleFor(c => c.Message).NotEmpty().MaximumLength(4000);
        RuleFor(c => c.SessionId).MaximumLength(128);
    }
}
