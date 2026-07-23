using MedicalAssistant.Transcriber.Models;

namespace MedicalAssistant.Transcriber.Services.Interfaces;

public interface IAuditTrailService
{
    Task LogAsync(
        string action,
        ConsultationProcessingMessage? message = null,
        object? details = null,
        string? entityType = null,
        string? entityId = null,
        CancellationToken cancellationToken = default);
}
