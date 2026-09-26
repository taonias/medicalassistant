using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Repositories;

public class ChatRequestRepository : GenericRepository<ChatRequest>, IChatRequestRepository
{
    public ChatRequestRepository(MedicalAssistantDatabaseContext context, IHttpContextAccessor httpContextAccessor)
        : base(context, httpContextAccessor)
    {
    }

    public async Task<ChatRequest?> GetByCorrelationIdAsync(string correlationId)
    {
        return await _context.ChatRequests
            .FirstOrDefaultAsync(c => c.CorrelationId == correlationId);
    }

    public async Task<ChatRequest?> GetByCorrelationIdForDoctorAsync(string correlationId, string doctorId)
    {
        return await _context.ChatRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CorrelationId == correlationId && c.DoctorId == doctorId);
    }
}
