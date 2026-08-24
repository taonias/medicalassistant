using FluentValidation;

namespace MedicalAssistant.Application.Features.Chat.Queries.ChatQuery;

public class ChatQueryValidator : AbstractValidator<ChatQuery>
{
    public ChatQueryValidator()
    {
        RuleFor(c => c.Message).NotEmpty().MaximumLength(4000);
    }
}
