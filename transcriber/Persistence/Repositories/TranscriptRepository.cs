using MedicalAssistant.Domain;
using MedicalAssistant.Transcriber.Persistence.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Transcriber.Persistence.Repositories;

public sealed class TranscriptRepository : ITranscriptRepository
{
    private readonly TranscriberDbContext _dbContext;

    public TranscriptRepository(TranscriberDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Transcript?> GetByConsultationIdAsync(
        int consultationId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Transcripts
            .FirstOrDefaultAsync(t => t.ConsultationId == consultationId, cancellationToken);
    }

    public async Task AddAsync(Transcript transcript, CancellationToken cancellationToken = default)
    {
        _dbContext.Transcripts.Add(transcript);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Transcript transcript, CancellationToken cancellationToken = default)
    {
        _dbContext.Transcripts.Update(transcript);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
