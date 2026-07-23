using System.Text.Json;
using MedicalAssistant.Domain;
using MedicalAssistant.Transcriber.Models;
using MedicalAssistant.Transcriber.Persistence.Repositories.Interfaces;
using MedicalAssistant.Transcriber.Services.Interfaces;

namespace MedicalAssistant.Transcriber.Services;

public sealed class AuditTrailService : IAuditTrailService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IAuditLogRepository _auditLogRepository;

    public AuditTrailService(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task LogAsync(
        string action,
        ConsultationProcessingMessage? message = null,
        object? details = null,
        string? entityType = null,
        string? entityId = null,
        CancellationToken cancellationToken = default)
    {
        var detailsJson = details is null
            ? null
            : JsonSerializer.Serialize(details, JsonOptions);

        if (detailsJson is { Length: > 2000 })
            detailsJson = detailsJson[..2000];

        await _auditLogRepository.AddAsync(
            new AuditLog
            {
                UserId = "transcriber-function",
                UserName = "MedicalAssistant.Transcriber",
                Action = action,
                EntityType = entityType
                    ?? (message is null ? "Transcriber" : "Consultation"),
                EntityId = entityId
                    ?? message?.ConsultationId.ToString()
                    ?? message?.CorrelationId,
                Details = detailsJson,
                Timestamp = DateTime.UtcNow,
            },
            cancellationToken);
    }
}
