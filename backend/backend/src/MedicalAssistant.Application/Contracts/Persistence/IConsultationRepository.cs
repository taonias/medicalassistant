using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface IConsultationRepository : IGenericRepository<Consultation>
{
    Task<Consultation?> GetConsultationForDoctorAsync(int id, string doctorId);
    Task<IReadOnlyList<Consultation>> GetConsultationsByPatientForDoctorAsync(int patientId, string doctorId);
    Task<IReadOnlyList<DraftConsultationListItem>> GetDraftConsultationsForDoctorAsync(string doctorId);
    Task<Consultation?> GetByIdempotencyKeyAsync(string idempotencyKey, string doctorId);
    Task<bool> IdempotencyKeyExistsAsync(string idempotencyKey, string doctorId);
}
