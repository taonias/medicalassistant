using MedicalAssistant.Domain;

namespace MedicalAssistant.Transcriber.Persistence.Repositories.Interfaces;

public interface IConsultationRepository
{
    Task<Consultation?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateAsync(Consultation consultation, CancellationToken cancellationToken = default);
}
