using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface ITranscriptRepository : IGenericRepository<Transcript>
{
    Task<Transcript?> GetByConsultationIdAsync(int consultationId);
    Task<Transcript?> GetByExternalJobIdAsync(string externalJobId);
    Task<Transcript> UpdateWithOutboxAsync(
        Transcript transcript,
        Consultation consultation,
        ConsultationOutboxMessage outboxMessage,
        CancellationToken cancellationToken = default);
    Task<Transcript> CompleteWithOutboxAsync(
        Transcript transcript,
        Consultation consultation,
        string correlationId,
        CancellationToken cancellationToken = default);
}
