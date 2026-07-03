using MedicalAssistant.Application.Contracts.AiModule;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Models.AiModule;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Notifications;

public class TranscriptionCompletedNotificationHandler : INotificationHandler<TranscriptionCompletedNotification>
{
    private readonly IAiModuleClient _aiModuleClient;
    private readonly IConsultationRepository _consultationRepository;
    private readonly AiModuleSettings _aiSettings;

    public TranscriptionCompletedNotificationHandler(
        IAiModuleClient aiModuleClient,
        IConsultationRepository consultationRepository,
        IOptions<AiModuleSettings> aiSettings)
    {
        _aiModuleClient = aiModuleClient;
        _consultationRepository = consultationRepository;
        _aiSettings = aiSettings.Value;
    }

    public async Task Handle(TranscriptionCompletedNotification notification, CancellationToken cancellationToken)
    {
        var consultation = await _consultationRepository.GetByIdAsync(notification.ConsultationId);
        consultation.MarkStructuredDataPending();
        await _consultationRepository.UpdateAsync(consultation);

        var callbackUrl = $"{_aiSettings.ApiBaseUrl.TrimEnd('/')}/api/ai-callback/structured-data";
        await _aiModuleClient.ExtractStructuredDataAsync(new StructuredDataJobRequest
        {
            ConsultationId = notification.ConsultationId,
            TranscriptText = notification.TranscriptText,
            SchemaVersion = "v1",
            CallbackUrl = callbackUrl,
            CorrelationId = Guid.NewGuid().ToString("N")
        }, cancellationToken);
    }
}
