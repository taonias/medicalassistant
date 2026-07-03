using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Repositories;

public class TranscriptRepository : GenericRepository<Transcript>, ITranscriptRepository
{
    public TranscriptRepository(MedicalAssistantDatabaseContext context, IHttpContextAccessor httpContextAccessor)
        : base(context, httpContextAccessor)
    {
    }

    public async Task<Transcript?> GetByConsultationIdAsync(int consultationId)
    {
        return await _context.Transcripts
            .FirstOrDefaultAsync(t => t.ConsultationId == consultationId);
    }

    public async Task<Transcript?> GetByExternalJobIdAsync(string externalJobId)
    {
        return await _context.Transcripts
            .FirstOrDefaultAsync(t => t.ExternalJobId == externalJobId);
    }
}
