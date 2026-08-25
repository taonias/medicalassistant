using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

/// <summary>
/// The persistence capability behind registering an uploaded file — audio or
/// document — against a consultation (R28): the doctor-scoped lookup that
/// authorizes the upload, and the durable write that commits the consultation's
/// new blob reference alongside its outbox notification, together. A narrow
/// slice of the broader <see cref="IConsultationRepository"/>, sibling to
/// <see cref="IConsultationDeletion"/> — see R28 for the rest.
/// </summary>
public interface IConsultationFileRegistration
{
    Task<Consultation?> GetConsultationForDoctorAsync(int id, string doctorId);

    Task<Consultation> UpdateWithOutboxAsync(
        Consultation consultation,
        ConsultationOutboxMessage outboxMessage,
        CancellationToken cancellationToken = default);
}
