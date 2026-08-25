using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

/// <summary>
/// The persistence capability behind consultation deletion (R28): the
/// doctor-scoped lookup that authorizes the action, and the durable write
/// that commits the tombstone, its cleanup record, and the outbox
/// notification together. A narrow first slice of the broader
/// <see cref="IConsultationRepository"/> — see R28 for the rest.
/// </summary>
public interface IConsultationDeletion
{
    Task<Consultation?> GetConsultationForDoctorAsync(int id, string doctorId);

    Task<Consultation> RecordDeletionAsync(
        Consultation consultation,
        ConsultationDeletionCleanup cleanup,
        ConsultationOutboxMessage outboxMessage,
        CancellationToken cancellationToken = default);
}
