using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface IMedicalStructuredDataRepository : IGenericRepository<MedicalStructuredData>
{
    Task<IReadOnlyList<MedicalStructuredData>> GetByConsultationIdAsync(int consultationId);
    Task<MedicalStructuredData?> GetLatestByConsultationIdAsync(int consultationId);
}
