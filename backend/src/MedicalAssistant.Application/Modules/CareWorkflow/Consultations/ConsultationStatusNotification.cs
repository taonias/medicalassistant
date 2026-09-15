namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations;

/// <summary>
/// A consultation's status (or doctor-visible failure reason) changed on the server
/// without the viewing doctor's own action — pushed so the detail page can refetch
/// instead of polling. Intentionally minimal: the client re-fetches the consultation
/// rather than trusting a duplicated status string over the wire.
/// </summary>
public sealed record ConsultationStatusChangedEvent(int ConsultationId, DateTimeOffset OccurredAtUtc)
{
    public static ConsultationStatusChangedEvent For(int consultationId) =>
        new(consultationId, DateTimeOffset.UtcNow);
}

/// <summary>
/// Outbound port: pushes a status-changed event to the owning doctor's live clients.
/// The SignalR implementation lives in the API layer. Delivery is best-effort — a
/// missed push just means the doctor sees the change on their next manual refresh.
/// </summary>
public interface IConsultationStatusNotifier
{
    Task NotifyAsync(
        string doctorId,
        ConsultationStatusChangedEvent statusChanged,
        CancellationToken cancellationToken = default);
}
