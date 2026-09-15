using MedicalAssistant.Application.Modules.CareWorkflow.Consultations;
using Microsoft.AspNetCore.SignalR;

namespace MedicalAssistant.Api.Realtime;

/// <summary>
/// Pushes a consultation status-changed ping to the owning doctor over
/// <see cref="ConsultationHub"/>. Best-effort: a delivery failure is logged and
/// swallowed so a lost push can never fail the state change it was describing — the
/// database write already committed by the time this runs.
/// </summary>
public sealed class SignalRConsultationStatusNotifier : IConsultationStatusNotifier
{
    private readonly IHubContext<ConsultationHub> _hub;
    private readonly ILogger<SignalRConsultationStatusNotifier> _logger;

    public SignalRConsultationStatusNotifier(
        IHubContext<ConsultationHub> hub,
        ILogger<SignalRConsultationStatusNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyAsync(
        string doctorId,
        ConsultationStatusChangedEvent statusChanged,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(doctorId))
        {
            return;
        }

        try
        {
            await _hub.Clients.User(doctorId).SendAsync(ConsultationHub.ClientMethod, statusChanged, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not push status change for consultation {ConsultationId}; the doctor's next refresh is unaffected",
                statusChanged.ConsultationId);
        }
    }
}
