using FluentValidation;

namespace MedicalAssistant.Application.Features.Chat.Queries.GetChatRequestStatus;

public class GetChatRequestStatusQueryValidator : AbstractValidator<GetChatRequestStatusQuery>
{
    public GetChatRequestStatusQueryValidator()
    {
        RuleFor(q => q.CorrelationId).NotEmpty().MaximumLength(64);
    }
}
