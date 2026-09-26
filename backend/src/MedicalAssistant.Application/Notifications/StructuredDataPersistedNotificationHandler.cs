using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Models.Messaging;
using MediatR;

namespace MedicalAssistant.Application.Notifications;

public class StructuredDataPersistedNotificationHandler : INotificationHandler<StructuredDataPersistedNotification>
{
    private readonly IAiRequestPublisher _aiRequestPublisher;
    private readonly IConsultationRepository _consultationRepository;
    private readonly ITranscriptRepository _transcriptRepository;
    private readonly IMedicalStructuredDataRepository _structuredDataRepository;
    private readonly IAppLogger<StructuredDataPersistedNotificationHandler> _logger;

    public StructuredDataPersistedNotificationHandler(
        IAiRequestPublisher aiRequestPublisher,
        IConsultationRepository consultationRepository,
        ITranscriptRepository transcriptRepository,
        IMedicalStructuredDataRepository structuredDataRepository,
        IAppLogger<StructuredDataPersistedNotificationHandler> logger)
    {
        _aiRequestPublisher = aiRequestPublisher;
        _consultationRepository = consultationRepository;
        _transcriptRepository = transcriptRepository;
        _structuredDataRepository = structuredDataRepository;
        _logger = logger;
    }

    public async Task Handle(StructuredDataPersistedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var consultation = await _consultationRepository.GetByIdAsync(notification.ConsultationId);
            if (consultation == null)
                return;

            var transcript = await _transcriptRepository.GetByConsultationIdAsync(notification.ConsultationId);
            var structured = await _structuredDataRepository.GetLatestByConsultationIdAsync(notification.ConsultationId);

            if (transcript?.TranscriptText == null || structured?.StructuredPayload == null)
                return;

            if (!consultation.PatientId.HasValue)
                return;

            var patientId = consultation.PatientId.Value;
            await _aiRequestPublisher.PublishIndexAsync(new IndexDocumentsRequestedMessage
            {
                EventType = AiRequestEventTypes.DocumentsIndexRequested,
                CorrelationId = Guid.NewGuid().ToString("N"),
                OccurredAtUtc = DateTime.UtcNow,
                Documents =
                [
                    new IndexDocumentMessage
                    {
                        Id = $"transcript:{consultation.Id}",
                        PatientId = patientId,
                        ConsultationId = consultation.Id,
                        DocType = "transcript",
                        Content = transcript.TranscriptText
                    },
                    new IndexDocumentMessage
                    {
                        Id = $"structured:{consultation.Id}",
                        PatientId = patientId,
                        ConsultationId = consultation.Id,
                        DocType = "structured",
                        Content = structured.StructuredPayload
                    }
                ]
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to queue indexing for consultation {ConsultationId}: {Error}", notification.ConsultationId, ex.Message);
        }
    }
}
