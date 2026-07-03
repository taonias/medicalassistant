using FluentValidation;
using MedicalAssistant.Application.Models;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Features.Consultation.Command.UploadConsultationAudio;

public class UploadConsultationAudioCommandValidator : AbstractValidator<UploadConsultationAudioCommand>
{
    public UploadConsultationAudioCommandValidator(IOptions<AppSettings> appSettings)
    {
        var settings = appSettings.Value;

        RuleFor(c => c.ConsultationId).GreaterThan(0);
        RuleFor(c => c.AudioFile).NotNull();
        RuleFor(c => c.AudioFile.Length)
            .GreaterThan(0)
            .LessThanOrEqualTo(settings.MaxAudioFileSizeBytes)
            .When(c => c.AudioFile != null);
        RuleFor(c => c.AudioFile.ContentType)
            .Must(ct => settings.AllowedAudioContentTypes.Contains(ct, StringComparer.OrdinalIgnoreCase))
            .When(c => c.AudioFile != null)
            .WithMessage("Unsupported audio content type.");
    }
}
