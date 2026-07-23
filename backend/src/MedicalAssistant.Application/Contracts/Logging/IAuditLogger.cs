namespace MedicalAssistant.Application.Contracts.Logging;

public interface IAuditLogger
{
    Task LogAsync(string action, string? entityType = null, string? entityId = null, string? details = null, string? overrideUserId = null, string? overrideUserName = null);
}
