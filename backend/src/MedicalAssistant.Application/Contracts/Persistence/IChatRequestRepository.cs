using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface IChatRequestRepository : IGenericRepository<ChatRequest>
{
    Task<ChatRequest?> GetByCorrelationIdAsync(string correlationId);
    Task<ChatRequest?> GetByCorrelationIdForDoctorAsync(string correlationId, string doctorId);
}
