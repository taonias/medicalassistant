using FluentValidation;

namespace MedicalAssistant.Application.Modules.Assistance.Chat.ChatQuery;

public class ChatQueryValidator : AbstractValidator<ChatQuery>
{
    public ChatQueryValidator()
    {
        RuleFor(c => c.Message).NotEmpty().MaximumLength(4000);
    }
}
