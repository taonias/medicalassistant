using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Repositories;

public class MedicalStructuredDataRepository : GenericRepository<MedicalStructuredData>, IMedicalStructuredDataRepository
{
    public MedicalStructuredDataRepository(MedicalAssistantDatabaseContext context, IHttpContextAccessor httpContextAccessor)
        : base(context, httpContextAccessor)
    {
    }

    public async Task<IReadOnlyList<MedicalStructuredData>> GetByConsultationIdAsync(int consultationId)
    {
        return await _context.MedicalStructuredData
            .AsNoTracking()
            .Where(m => m.ConsultationId == consultationId)
            .OrderByDescending(m => m.ExtractedAt)
            .ToListAsync();
    }

    public async Task<MedicalStructuredData?> GetLatestByConsultationIdAsync(int consultationId)
    {
        return await _context.MedicalStructuredData
            .AsNoTracking()
            .Where(m => m.ConsultationId == consultationId)
            .OrderByDescending(m => m.ExtractedAt)
            .FirstOrDefaultAsync();
    }
}
