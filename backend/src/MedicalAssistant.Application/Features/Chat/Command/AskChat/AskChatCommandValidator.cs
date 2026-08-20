using FluentValidation;

namespace MedicalAssistant.Application.Features.Chat.Command.AskChat;

public sealed class AskChatCommandValidator : AbstractValidator<AskChatCommand>
{
    public AskChatCommandValidator()
    {
        RuleFor(c => c.Question).NotEmpty().MaximumLength(4000);

        // At least one anchor is required unless appending to an existing conversation.
        RuleFor(c => c)
            .Must(c => c.ConversationId.HasValue || c.PatientId.HasValue || c.ConsultationId.HasValue)
            .WithMessage("A conversationId, patientId, or consultationId is required.");
    }
}
