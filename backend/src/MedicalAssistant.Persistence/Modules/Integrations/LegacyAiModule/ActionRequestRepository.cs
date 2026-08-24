using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Repositories;

public class ActionRequestRepository : GenericRepository<ActionRequest>, IActionRequestRepository
{
    public ActionRequestRepository(MedicalAssistantDatabaseContext context, IHttpContextAccessor httpContextAccessor)
        : base(context, httpContextAccessor)
    {
    }

    public async Task<ActionRequest?> GetByCorrelationIdAsync(string correlationId)
    {
        return await _context.ActionRequests
            .FirstOrDefaultAsync(a => a.CorrelationId == correlationId);
    }

    public async Task<ActionRequest?> GetByCorrelationIdForDoctorAsync(string correlationId, string doctorId)
    {
        return await _context.ActionRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CorrelationId == correlationId && a.DoctorId == doctorId);
    }

    public async Task<bool> CorrelationIdExistsAsync(string correlationId)
    {
        return await _context.ActionRequests.AnyAsync(a => a.CorrelationId == correlationId);
    }
}
