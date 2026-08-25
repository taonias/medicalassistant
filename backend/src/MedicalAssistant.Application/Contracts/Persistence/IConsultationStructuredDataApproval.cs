using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

/// <summary>
/// The persistence capability behind approving a consultation's structured
/// data (R28): the doctor-scoped lookup that authorizes the approval, and the
/// durable write that marks the consultation Completed once approved. See R28
/// for the rest of the sibling ports.
/// </summary>
public interface IConsultationStructuredDataApproval
{
    Task<Consultation?> GetConsultationForDoctorAsync(int id, string doctorId);

    Task<Consultation> UpdateAsync(Consultation consultation);
}
