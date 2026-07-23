using MedicalAssistant.Domain;
using MedicalAssistant.Transcriber.Persistence.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Transcriber.Persistence.Repositories;

public sealed class ConsultationRepository : IConsultationRepository
{
    private readonly TranscriberDbContext _dbContext;

    public ConsultationRepository(TranscriberDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Consultation?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Consultations
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Consultation consultation, CancellationToken cancellationToken = default)
    {
        _dbContext.Consultations.Update(consultation);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
