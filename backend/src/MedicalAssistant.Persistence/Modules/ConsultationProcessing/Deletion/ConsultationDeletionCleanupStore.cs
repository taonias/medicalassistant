using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Modules.ConsultationProcessing.Deletion;

public sealed class ConsultationDeletionCleanupStore : IConsultationDeletionCleanupStore
{
    private readonly MedicalAssistantDatabaseContext _context;

    public ConsultationDeletionCleanupStore(MedicalAssistantDatabaseContext context)
    {
        _context = context;
    }

    public async Task<ConsultationDeletionCleanupPlan> GetPlanAsync(
        IntegrationEventEnvelope<ConsultationDeletedV1> envelope,
        CancellationToken cancellationToken = default)
    {
        var cleanup = await _context.ConsultationDeletionCleanups
            .SingleOrDefaultAsync(c => c.DeletionEventId == envelope.EventId, cancellationToken)
            ?? throw new NotFoundException(nameof(ConsultationDeletionCleanup), envelope.EventId);

        var consultation = await _context.Consultations
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == envelope.Payload.ConsultationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Consultation), envelope.Payload.ConsultationId);

        var blobReferences = new[]
            {
                consultation.SourceObjectReference,
                consultation.AudioBlobUri,
                consultation.DocumentBlobUri
            }
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => reference!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var documentIds = await _context.ConsultationInboxMessages
            .AsNoTracking()
            .Where(message => message.ClinicalKnowledgeDocumentId != null)
            .Select(message => message.ClinicalKnowledgeDocumentId!)
            .ToListAsync(cancellationToken);
        var consultationDocumentIds = documentIds
            .Where(documentId => IsDocumentForConsultation(documentId, envelope.Payload.ConsultationId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new ConsultationDeletionCleanupPlan(
            cleanup.ConsultationId,
            consultation.DeletedBy ?? "system",
            cleanup.BlobCleanupStatus,
            blobReferences,
            cleanup.ClinicalKnowledgeCleanupStatus,
            consultationDocumentIds);
    }

    public Task MarkBlobCleanupAsync(
        IntegrationEventEnvelope<ConsultationDeletedV1> envelope,
        ConsultationCleanupStatus status,
        CancellationToken cancellationToken = default) =>
        MarkAsync(
            envelope,
            cleanup =>
            {
                cleanup.BlobCleanupStatus = status;
                cleanup.BlobCleanupCompletedAtUtc =
                    status is ConsultationCleanupStatus.Completed or ConsultationCleanupStatus.NotRequired
                        ? DateTime.UtcNow
                        : null;
            },
            cancellationToken);

    public Task MarkClinicalKnowledgeCleanupAsync(
        IntegrationEventEnvelope<ConsultationDeletedV1> envelope,
        ConsultationCleanupStatus status,
        CancellationToken cancellationToken = default) =>
        MarkAsync(
            envelope,
            cleanup =>
            {
                cleanup.ClinicalKnowledgeCleanupStatus = status;
                cleanup.ClinicalKnowledgeCleanupCompletedAtUtc =
                    status is ConsultationCleanupStatus.Completed or ConsultationCleanupStatus.NotRequired
                        ? DateTime.UtcNow
                        : null;
            },
            cancellationToken);

    private async Task MarkAsync(
        IntegrationEventEnvelope<ConsultationDeletedV1> envelope,
        Action<ConsultationDeletionCleanup> apply,
        CancellationToken cancellationToken)
    {
        var cleanup = await _context.ConsultationDeletionCleanups
            .SingleOrDefaultAsync(c => c.DeletionEventId == envelope.EventId, cancellationToken)
            ?? throw new NotFoundException(nameof(ConsultationDeletionCleanup), envelope.EventId);

        cleanup.AttemptCount++;
        cleanup.LastFailureCategory = null;
        cleanup.LastFailureCode = null;
        apply(cleanup);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsDocumentForConsultation(string documentId, int consultationId)
    {
        var parts = documentId.Split('#', StringSplitOptions.TrimEntries);
        return parts.Length == 4 &&
               string.Equals(parts[2], consultationId.ToString(), StringComparison.Ordinal);
    }
}
