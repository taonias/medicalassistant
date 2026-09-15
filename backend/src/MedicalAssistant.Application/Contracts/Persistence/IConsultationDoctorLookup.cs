namespace MedicalAssistant.Application.Contracts.Persistence;

/// <summary>
/// The one thing a system-triggered handler (an integration-event consumer, not a
/// doctor's own request) needs before it can push a real-time notification: whose
/// doctor a consultation belongs to. Deliberately not doctor-scoped like
/// <see cref="IConsultationAccess"/> — the caller doesn't have a doctorId yet, that's
/// the point of asking.
/// </summary>
public interface IConsultationDoctorLookup
{
    Task<string?> GetDoctorIdAsync(int consultationId, CancellationToken cancellationToken = default);
}
