using FluentValidation;

namespace MedicalAssistant.Application.Features.DoctorNotes.Command.CreateDoctorNote;

public class CreateDoctorNoteCommandValidator : AbstractValidator<CreateDoctorNoteCommand>
{
    public CreateDoctorNoteCommandValidator()
    {
        RuleFor(c => c.Content).NotEmpty();
        RuleFor(c => c)
            .Must(c => c.ConsultationId.HasValue || c.PatientId.HasValue)
            .WithMessage("Either ConsultationId or PatientId must be provided.");
    }
}
