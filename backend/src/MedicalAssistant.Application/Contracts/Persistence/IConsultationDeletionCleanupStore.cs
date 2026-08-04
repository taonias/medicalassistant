using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface IConsultationDeletionCleanupStore
{
    Task<ConsultationDeletionCleanupPlan> GetPlanAsync(
        IntegrationEventEnvelope<ConsultationDeletedV1> envelope,
        CancellationToken cancellationToken = default);

    Task MarkBlobCleanupAsync(
        IntegrationEventEnvelope<ConsultationDeletedV1> envelope,
        ConsultationCleanupStatus status,
        CancellationToken cancellationToken = default);

    Task MarkClinicalKnowledgeCleanupAsync(
        IntegrationEventEnvelope<ConsultationDeletedV1> envelope,
        ConsultationCleanupStatus status,
        CancellationToken cancellationToken = default);
}

public sealed record ConsultationDeletionCleanupPlan(
    int ConsultationId,
    string RemovedBy,
    ConsultationCleanupStatus BlobCleanupStatus,
    IReadOnlyList<string> BlobObjectReferences,
    ConsultationCleanupStatus ClinicalKnowledgeCleanupStatus,
    IReadOnlyList<string> ClinicalKnowledgeDocumentIds);
