using MedicalAssistant.Application.Contracts.ClinicalKnowledge;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Contracts.Storage;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Application.EventHandlers;

public sealed class ConsultationDeletedIntegrationEventHandler
    : IIntegrationEventHandler<ConsultationDeletedV1>
{
    public const string ConsumerName = "backend-deletion-cleanup";

    private readonly IConsultationDeletionCleanupStore _cleanupStore;
    private readonly IConsultationBlobCleanupService _blobCleanupService;
    private readonly IClinicalKnowledgeDeletionGateway _clinicalKnowledgeClient;
    private readonly ILogger<ConsultationDeletedIntegrationEventHandler> _logger;

    public ConsultationDeletedIntegrationEventHandler(
        IConsultationDeletionCleanupStore cleanupStore,
        IConsultationBlobCleanupService blobCleanupService,
        IClinicalKnowledgeDeletionGateway clinicalKnowledgeClient,
        ILogger<ConsultationDeletedIntegrationEventHandler> logger)
    {
        _cleanupStore = cleanupStore;
        _blobCleanupService = blobCleanupService;
        _clinicalKnowledgeClient = clinicalKnowledgeClient;
        _logger = logger;
    }

    public async Task HandleAsync(
        IntegrationEventEnvelope<ConsultationDeletedV1> envelope,
        CancellationToken cancellationToken)
    {
        var plan = await _cleanupStore.GetPlanAsync(envelope, cancellationToken);

        if (plan.BlobCleanupStatus is not ConsultationCleanupStatus.Completed
            and not ConsultationCleanupStatus.NotRequired)
        {
            if (plan.BlobObjectReferences.Count == 0)
            {
                await _cleanupStore.MarkBlobCleanupAsync(
                    envelope,
                    ConsultationCleanupStatus.NotRequired,
                    cancellationToken);
            }
            else
            {
                await _blobCleanupService.DeleteIfExistsAsync(
                    plan.BlobObjectReferences,
                    cancellationToken);
                await _cleanupStore.MarkBlobCleanupAsync(
                    envelope,
                    ConsultationCleanupStatus.Completed,
                    cancellationToken);
            }
        }

        if (plan.ClinicalKnowledgeCleanupStatus is not ConsultationCleanupStatus.Completed
            and not ConsultationCleanupStatus.NotRequired)
        {
            if (plan.ClinicalKnowledgeDocumentIds.Count == 0)
            {
                await _cleanupStore.MarkClinicalKnowledgeCleanupAsync(
                    envelope,
                    ConsultationCleanupStatus.NotRequired,
                    cancellationToken);
            }
            else
            {
                foreach (var documentId in plan.ClinicalKnowledgeDocumentIds)
                {
                    await _clinicalKnowledgeClient.UnIngestDocumentAsync(
                        documentId,
                        plan.RemovedBy,
                        cancellationToken);
                }

                await _cleanupStore.MarkClinicalKnowledgeCleanupAsync(
                    envelope,
                    ConsultationCleanupStatus.Completed,
                    cancellationToken);
            }
        }

        _logger.LogInformation(
            "Deletion cleanup converged for consultation {ConsultationId}.",
            envelope.Payload.ConsultationId);
    }
}
