using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface IDoctorNoteRepository : IGenericRepository<DoctorNote>
{
    Task<IReadOnlyList<DoctorNote>> GetByConsultationIdForDoctorAsync(int consultationId, string doctorId);

    /// <summary>
    /// Notes attached to a patient only (no consultation).
    /// </summary>
    Task<IReadOnlyList<DoctorNote>> GetPatientLevelNotesForDoctorAsync(int patientId, string doctorId);
}
