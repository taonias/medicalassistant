using MedicalAssistant.Domain;

namespace MedicalAssistant.Transcriber.Persistence.Repositories.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
}
