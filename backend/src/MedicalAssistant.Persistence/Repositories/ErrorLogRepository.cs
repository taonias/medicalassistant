using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Domain;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Repositories;

public class ErrorLogRepository : IErrorLogRepository
{
    private readonly MedicalAssistantDatabaseContext _context;

    public ErrorLogRepository(MedicalAssistantDatabaseContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<ErrorLog> Items, int TotalCount)> GetPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ErrorLogs.AsNoTracking().OrderByDescending(log => log.Timestamp);
        var totalCount = await query.CountAsync(cancellationToken);
        page = Paging.ClampPage(page, pageSize, totalCount);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
