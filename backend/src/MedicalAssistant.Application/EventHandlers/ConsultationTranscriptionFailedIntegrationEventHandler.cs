using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Modules.CareWorkflow.Consultations;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Application.EventHandlers;

/// <summary>
/// The transcription worker already committed the Failed status and failure reason
/// directly to the shared database before publishing this event (see
/// TranscriptionCompletionUnitOfWork.FailAsync) — the worker is a separate deployable
/// with no connection to this process's SignalR clients. This handler's only job is to
/// tell the owning doctor's open browser tab to refetch; it mutates nothing.
/// </summary>
public sealed class ConsultationTranscriptionFailedIntegrationEventHandler
    : IIntegrationEventHandler<ConsultationTranscriptionFailedV1>
{
    private readonly IConsultationDoctorLookup _doctorLookup;
    private readonly IConsultationStatusNotifier _notifier;
    private readonly ILogger<ConsultationTranscriptionFailedIntegrationEventHandler> _logger;

    public ConsultationTranscriptionFailedIntegrationEventHandler(
        IConsultationDoctorLookup doctorLookup,
        IConsultationStatusNotifier notifier,
        ILogger<ConsultationTranscriptionFailedIntegrationEventHandler> logger)
    {
        _doctorLookup = doctorLookup;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task HandleAsync(
        IntegrationEventEnvelope<ConsultationTranscriptionFailedV1> envelope,
        CancellationToken cancellationToken)
    {
        var consultationId = envelope.Payload.ConsultationId;
        var doctorId = await _doctorLookup.GetDoctorIdAsync(consultationId, cancellationToken);

        if (doctorId is null)
        {
            _logger.LogWarning(
                "Skipping transcription-failed push for unknown consultation {ConsultationId}.",
                consultationId);
            return;
        }

        await _notifier.NotifyAsync(
            doctorId,
            ConsultationStatusChangedEvent.For(consultationId),
            cancellationToken);
    }
}
