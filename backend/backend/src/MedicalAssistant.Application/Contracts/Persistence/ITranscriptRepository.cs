using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface ITranscriptRepository : IGenericRepository<Transcript>
{
    Task<Transcript?> GetByConsultationIdAsync(int consultationId);
    Task<Transcript?> GetByExternalJobIdAsync(string externalJobId);
}
