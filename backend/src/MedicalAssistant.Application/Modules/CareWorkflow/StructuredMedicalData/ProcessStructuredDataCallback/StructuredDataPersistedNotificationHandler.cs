using MedicalAssistant.Application.Contracts.AiModule;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Models.AiModule;
using MedicalAssistant.Domain;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Application.Notifications;

public class StructuredDataPersistedNotificationHandler : INotificationHandler<StructuredDataPersistedNotification>
{
    private readonly IAiModuleClient _aiModuleClient;
    private readonly IStructuredDataCompletion _consultationRepository;
    private readonly ITranscriptRepository _transcriptRepository;
    private readonly IMedicalStructuredDataRepository _structuredDataRepository;
    private readonly ILogger<StructuredDataPersistedNotificationHandler> _logger;

    public StructuredDataPersistedNotificationHandler(
        IAiModuleClient aiModuleClient,
        IStructuredDataCompletion consultationRepository,
        ITranscriptRepository transcriptRepository,
        IMedicalStructuredDataRepository structuredDataRepository,
        ILogger<StructuredDataPersistedNotificationHandler> logger)
    {
        _aiModuleClient = aiModuleClient;
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
            var req = new IndexDocumentsJobRequest
            {
                Documents =
                [
                    new IndexDocument
                    {
                        Id = $"transcript:{consultation.Id}",
                        PatientId = patientId,
                        ConsultationId = consultation.Id,
                        DocType = "transcript",
                        Content = transcript.TranscriptText
                    },
                    new IndexDocument
                    {
                        Id = $"structured:{consultation.Id}",
                        PatientId = patientId,
                        ConsultationId = consultation.Id,
                        DocType = "structured",
                        Content = structured.StructuredPayload
                    }
                ]
            };

            await _aiModuleClient.IndexDocumentsAsync(req, cancellationToken);
        }
        catch (Exception ex)
        {
            // Indexing must not break the core clinical workflow.
            _logger.LogError(ex, "Failed to index clinical documents for consultation {ConsultationId}", notification.ConsultationId);
        }
    }
}
