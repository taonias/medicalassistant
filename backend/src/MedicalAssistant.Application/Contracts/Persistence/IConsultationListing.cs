using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

/// <summary>
/// The persistence capability behind listing a doctor's or a patient's
/// consultations (R28): five doctor-scoped reads, none with a side effect or a
/// transaction to protect — a different kind of capability from
/// <see cref="IConsultationDeletion"/>, <see cref="IConsultationFileRegistration"/>,
/// and <see cref="ITranscriptionCompletion"/>, which each exist to guard a
/// write. Grouped into one port rather than five, because what separates them
/// from the broader <see cref="IConsultationRepository"/> is the same thing for
/// all five: read-only, doctor-scoped, no write. See R28 for the rest.
/// </summary>
public interface IConsultationListing
{
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

    Task<IReadOnlyList<Consultation>> GetUnattachedDraftConsultationsForDoctorAsync(string doctorId);

    Task<IReadOnlyList<Consultation>> GetConsultationsForDoctorAsync(string doctorId);
}
