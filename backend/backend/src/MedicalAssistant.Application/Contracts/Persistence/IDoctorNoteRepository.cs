using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface IDoctorNoteRepository : IGenericRepository<DoctorNote>
{
    Task<IReadOnlyList<DoctorNote>> GetByConsultationIdForDoctorAsync(int consultationId, string doctorId);
}
