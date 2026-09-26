using FluentValidation;

namespace MedicalAssistant.Application.Features.Chat.Command.ProcessChatResult;

public class ProcessChatResultCommandValidator : AbstractValidator<ProcessChatResultCommand>
{
    public ProcessChatResultCommandValidator()
    {
        RuleFor(c => c.CorrelationId).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Status).NotEmpty();
    }
}
