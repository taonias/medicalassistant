using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

/// <summary>
/// The persistence capability behind assigning a patient to a previously
/// unassigned consultation (R28): the doctor-scoped lookup that authorizes the
/// assignment, and the durable write that commits it. See R28 for the rest of
/// the sibling ports.
/// </summary>
public interface IConsultationPatientAssignment
{
    Task<Consultation?> GetConsultationForDoctorAsync(int id, string doctorId);

    Task<Consultation> UpdateAsync(Consultation consultation);
}
