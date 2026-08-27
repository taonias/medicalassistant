using FluentValidation;
using MedicalAssistant.Application.Models;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations.UploadConsultationDocument;

public class UploadConsultationDocumentCommandValidator : AbstractValidator<UploadConsultationDocumentCommand>
{
    public UploadConsultationDocumentCommandValidator(IOptions<AppSettings> appSettings)
    {
        var settings = appSettings.Value;

        RuleFor(c => c.ConsultationId).GreaterThan(0);
        RuleFor(c => c.DocumentFile).NotNull();
        RuleFor(c => c.DocumentFile.Length)
            .GreaterThan(0)
            .LessThanOrEqualTo(settings.MaxDocumentFileSizeBytes)
            .When(c => c.DocumentFile != null);
        RuleFor(c => c.DocumentFile)
            .Must(file =>
            {
                if (file == null) return false;
                if (settings.AllowedDocumentContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                    return true;
                return file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            })
            .When(c => c.DocumentFile != null)
            .WithMessage("Unsupported document content type. Only PDF is allowed.");
    }
}
