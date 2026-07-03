using MedicalAssistant.Application.Contracts.AiModule;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Models.AiModule;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Notifications;

public class ConsultationAudioUploadedNotificationHandler : INotificationHandler<ConsultationAudioUploadedNotification>
{
    private readonly IAiModuleClient _aiModuleClient;
    private readonly IConsultationRepository _consultationRepository;
    private readonly ITranscriptRepository _transcriptRepository;
    private readonly AiModuleSettings _aiSettings;

    public ConsultationAudioUploadedNotificationHandler(
        IAiModuleClient aiModuleClient,
        IConsultationRepository consultationRepository,
        ITranscriptRepository transcriptRepository,
        IOptions<AiModuleSettings> aiSettings)
    {
        _aiModuleClient = aiModuleClient;
        _consultationRepository = consultationRepository;
        _transcriptRepository = transcriptRepository;
        _aiSettings = aiSettings.Value;
    }

    public async Task Handle(ConsultationAudioUploadedNotification notification, CancellationToken cancellationToken)
    {
        var consultation = await _consultationRepository.GetByIdAsync(notification.ConsultationId);
        consultation.MarkTranscribing();
        await _consultationRepository.UpdateAsync(consultation);

        var transcript = await _transcriptRepository.GetByConsultationIdAsync(notification.ConsultationId);
        if (transcript == null)
        {
            transcript = new Domain.Transcript { ConsultationId = notification.ConsultationId };
            await _transcriptRepository.CreateAsync(transcript);
        }

        var callbackUrl = $"{_aiSettings.ApiBaseUrl.TrimEnd('/')}/api/ai-callback/transcription";
        var response = await _aiModuleClient.StartTranscriptionAsync(new TranscriptionJobRequest
        {
            ConsultationId = notification.ConsultationId,
            AudioBlobUri = notification.AudioBlobUri,
            CallbackUrl = callbackUrl,
            CorrelationId = notification.CorrelationId
        }, cancellationToken);

        transcript.MarkProcessing(response.JobId);
        await _transcriptRepository.UpdateAsync(transcript);
    }
}
