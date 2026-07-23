using MedicalAssistant.Domain;
using MedicalAssistant.Transcriber.Persistence.Repositories.Interfaces;

namespace MedicalAssistant.Transcriber.Persistence.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly TranscriberDbContext _dbContext;

    public AuditLogRepository(TranscriberDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
