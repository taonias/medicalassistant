using MedicalAssistant.Application.Models.Patients;
using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface IPatientRepository : IGenericRepository<Patient>
{
    Task<IReadOnlyList<Patient>> GetPatientsForDoctorAsync(string doctorId);
    Task<IReadOnlyList<PatientListItem>> GetPatientListForDoctorAsync(string doctorId);
    Task<Patient?> GetPatientForDoctorAsync(int id, string doctorId);
    Task<bool> IsExternalPatientIdUniqueAsync(string externalPatientId, string doctorId, int? excludeId = null);
}
