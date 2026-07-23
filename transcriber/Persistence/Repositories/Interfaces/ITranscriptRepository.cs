using MedicalAssistant.Domain;

namespace MedicalAssistant.Transcriber.Persistence.Repositories.Interfaces;

public interface ITranscriptRepository
{
    Task<Transcript?> GetByConsultationIdAsync(int consultationId, CancellationToken cancellationToken = default);
    Task AddAsync(Transcript transcript, CancellationToken cancellationToken = default);
    Task UpdateAsync(Transcript transcript, CancellationToken cancellationToken = default);
}
