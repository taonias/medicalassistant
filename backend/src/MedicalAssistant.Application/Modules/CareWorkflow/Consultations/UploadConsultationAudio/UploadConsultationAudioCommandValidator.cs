using FluentValidation;
using MedicalAssistant.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Features.Consultation.Command.UploadConsultationAudio;

public class UploadConsultationAudioCommandValidator : AbstractValidator<UploadConsultationAudioCommand>
{
    private static readonly string[] AllowedExtensions = [".webm", ".wav", ".mp3", ".mpeg", ".ogg"];

    public UploadConsultationAudioCommandValidator(IOptions<AppSettings> appSettings)
    {
        var settings = appSettings.Value;

        RuleFor(c => c.ConsultationId).GreaterThan(0);
        RuleFor(c => c.AudioFile).NotNull();
        RuleFor(c => c.AudioFile.Length)
            .GreaterThan(0)
            .LessThanOrEqualTo(settings.MaxAudioFileSizeBytes)
            .When(c => c.AudioFile != null);
        RuleFor(c => c.AudioFile)
            .Must(file => IsAllowedAudio(file, settings))
            .When(c => c.AudioFile != null)
            .WithMessage("Unsupported audio content type.");
    }

    private static bool IsAllowedAudio(IFormFile file, AppSettings settings)
    {
        var contentType = (file.ContentType ?? string.Empty).Split(';', 2)[0].Trim();
        if (!string.IsNullOrEmpty(contentType)
            && settings.AllowedAudioContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(file.FileName);
        return AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
