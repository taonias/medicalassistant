using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface IConsultationRepository : IGenericRepository<Consultation>
{
    Task<Consultation?> GetConsultationForDoctorAsync(int id, string doctorId);
    Task<IReadOnlyList<Consultation>> GetConsultationsByPatientForDoctorAsync(int patientId, string doctorId);
    Task<(IReadOnlyList<Consultation> Items, int TotalCount)> GetConsultationsByPatientForDoctorPageAsync(
        int patientId,
        string doctorId,
        DateTime? fromDate,
        DateTime? toDate,
        string? source,
        int page,
        int pageSize);
    Task<IReadOnlyList<DraftConsultationListItem>> GetDraftConsultationsForDoctorAsync(string doctorId);
    Task<Consultation?> GetByIdempotencyKeyAsync(string idempotencyKey, string doctorId);
    Task<bool> IdempotencyKeyExistsAsync(string idempotencyKey, string doctorId);
    Task<Consultation> UpdateWithOutboxAsync(
        Consultation consultation,
        ConsultationOutboxMessage outboxMessage,
        CancellationToken cancellationToken = default);
    Task<Consultation> RecordDeletionAsync(
        Consultation consultation,
        ConsultationDeletionCleanup cleanup,
        ConsultationOutboxMessage outboxMessage,
        CancellationToken cancellationToken = default);
    Task DeleteForDoctorAsync(Consultation consultation);
    Task<IReadOnlyList<Consultation>> GetUnattachedDraftConsultationsForDoctorAsync(string doctorId);
    Task<IReadOnlyList<Consultation>> GetConsultationsForDoctorAsync(string doctorId);
}
