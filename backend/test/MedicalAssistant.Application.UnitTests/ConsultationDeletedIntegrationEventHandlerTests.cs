using MedicalAssistant.Application.Contracts.ClinicalKnowledge;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Contracts.Storage;
using MedicalAssistant.Application.EventHandlers;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace MedicalAssistant.Application.UnitTests;

public class ConsultationDeletedIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_deletes_blob_refs_uningests_documents_and_marks_resources_completed()
    {
        var envelope = CreateEnvelope();
        var store = new RecordingDeletionCleanupStore(new ConsultationDeletionCleanupPlan(
            ConsultationId: 10,
            RemovedBy: "doctor-1",
            BlobCleanupStatus: ConsultationCleanupStatus.Pending,
            BlobObjectReferences: ["private://consultations/10/audio"],
            ClinicalKnowledgeCleanupStatus: ConsultationCleanupStatus.Pending,
            ClinicalKnowledgeDocumentIds: ["doctor-1#patient-1#10#2"]));
        var blobCleanup = new RecordingBlobCleanupService();
        var clinicalKnowledge = new RecordingClinicalKnowledgeClient();
        var handler = new ConsultationDeletedIntegrationEventHandler(
            store,
            blobCleanup,
            clinicalKnowledge,
            NullLogger<ConsultationDeletedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(envelope, CancellationToken.None);

        Assert.Equal(envelope.EventId, store.Envelope!.EventId);
        Assert.Equal(["private://consultations/10/audio"], blobCleanup.ObjectReferences);
        Assert.Equal(ConsultationCleanupStatus.Completed, store.BlobStatus);
        Assert.Equal("doctor-1#patient-1#10#2", Assert.Single(clinicalKnowledge.UnIngestedDocumentIds));
        Assert.Equal("doctor-1", clinicalKnowledge.RemovedBy);
        Assert.Equal(ConsultationCleanupStatus.Completed, store.ClinicalKnowledgeStatus);
    }

    [Fact]
    public async Task HandleAsync_marks_not_required_when_no_resources_exist()
    {
        var envelope = CreateEnvelope();
        var store = new RecordingDeletionCleanupStore(new ConsultationDeletionCleanupPlan(
            ConsultationId: 10,
            RemovedBy: "doctor-1",
            BlobCleanupStatus: ConsultationCleanupStatus.Pending,
            BlobObjectReferences: [],
            ClinicalKnowledgeCleanupStatus: ConsultationCleanupStatus.Pending,
            ClinicalKnowledgeDocumentIds: []));
        var blobCleanup = new RecordingBlobCleanupService();
        var clinicalKnowledge = new RecordingClinicalKnowledgeClient();
        var handler = new ConsultationDeletedIntegrationEventHandler(
            store,
            blobCleanup,
            clinicalKnowledge,
            NullLogger<ConsultationDeletedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(envelope, CancellationToken.None);

        Assert.Empty(blobCleanup.ObjectReferences);
        Assert.Empty(clinicalKnowledge.UnIngestedDocumentIds);
        Assert.Equal(ConsultationCleanupStatus.NotRequired, store.BlobStatus);
        Assert.Equal(ConsultationCleanupStatus.NotRequired, store.ClinicalKnowledgeStatus);
    }

    private sealed class RecordingDeletionCleanupStore : IConsultationDeletionCleanupStore
    {
        private readonly ConsultationDeletionCleanupPlan _plan;

        public RecordingDeletionCleanupStore(ConsultationDeletionCleanupPlan plan)
        {
            _plan = plan;
        }

        public IntegrationEventEnvelope<ConsultationDeletedV1>? Envelope { get; private set; }
        public ConsultationCleanupStatus? BlobStatus { get; private set; }
        public ConsultationCleanupStatus? ClinicalKnowledgeStatus { get; private set; }

        public Task<ConsultationDeletionCleanupPlan> GetPlanAsync(
            IntegrationEventEnvelope<ConsultationDeletedV1> envelope,
            CancellationToken cancellationToken = default)
        {
            Envelope = envelope;
            return Task.FromResult(_plan);
        }

        public Task MarkBlobCleanupAsync(
            IntegrationEventEnvelope<ConsultationDeletedV1> envelope,
            ConsultationCleanupStatus status,
            CancellationToken cancellationToken = default)
        {
            BlobStatus = status;
            return Task.CompletedTask;
        }

        public Task MarkClinicalKnowledgeCleanupAsync(
            IntegrationEventEnvelope<ConsultationDeletedV1> envelope,
            ConsultationCleanupStatus status,
            CancellationToken cancellationToken = default)
        {
            ClinicalKnowledgeStatus = status;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingBlobCleanupService : IConsultationBlobCleanupService
    {
        public IReadOnlyList<string> ObjectReferences { get; private set; } = [];

        public Task DeleteIfExistsAsync(
            IReadOnlyCollection<string> objectReferences,
            CancellationToken cancellationToken = default)
        {
            ObjectReferences = objectReferences.ToArray();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingClinicalKnowledgeClient : IClinicalKnowledgeClient
    {
        public IReadOnlyList<string> UnIngestedDocumentIds { get; private set; } = [];
        public string? RemovedBy { get; private set; }

        public Task<ClinicalKnowledgeIngestionAccepted> SubmitSessionTranscriptAsync(
            ClinicalKnowledgeSessionTranscriptRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ClinicalKnowledgeIngestionAccepted(Guid.NewGuid(), Duplicate: false));

        public Task<ClinicalKnowledgeUnIngestResult> UnIngestDocumentAsync(
            string documentId,
            string removedBy,
            CancellationToken cancellationToken = default)
        {
            UnIngestedDocumentIds = [.. UnIngestedDocumentIds, documentId];
            RemovedBy = removedBy;
            return Task.FromResult(new ClinicalKnowledgeUnIngestResult(
                documentId,
                ClinicalKnowledgeUnIngestStatus.Removed));
        }

        public Task<ClinicalKnowledgeAnswer> GetGroundedAnswerAsync(
            ClinicalKnowledgeChatRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private static IntegrationEventEnvelope<ConsultationDeletedV1> CreateEnvelope()
    {
        return new IntegrationEventEnvelope<ConsultationDeletedV1>(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            ConsultationIntegrationEvents.DeletedV1,
            1,
            DateTime.UtcNow,
            "medicalassistant.backend",
            "correlation-1",
            null,
            new ConsultationDeletedV1(
                10,
                DateTime.UtcNow,
                "doctor-delete"));
    }
}
