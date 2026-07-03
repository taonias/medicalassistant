using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface IActionRequestRepository : IGenericRepository<ActionRequest>
{
    Task<ActionRequest?> GetByCorrelationIdAsync(string correlationId);
    Task<ActionRequest?> GetByCorrelationIdForDoctorAsync(string correlationId, string doctorId);
    Task<bool> CorrelationIdExistsAsync(string correlationId);
}
