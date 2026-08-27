using FluentValidation;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Transcripts.UpdateTranscript;

public class UpdateTranscriptCommandValidator : AbstractValidator<UpdateTranscriptCommand>
{
    public UpdateTranscriptCommandValidator()
    {
        RuleFor(x => x.ConsultationId).GreaterThan(0);
        RuleFor(x => x.Transcript)
            .NotEmpty()
            .MaximumLength(500_000);
    }
}
