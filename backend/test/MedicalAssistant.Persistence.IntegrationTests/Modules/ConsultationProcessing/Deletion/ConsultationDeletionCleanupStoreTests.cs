using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.Persistence.DatabaseContext;
using MedicalAssistant.Persistence.Modules.ConsultationProcessing.Deletion;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MedicalAssistant.Persistence.IntegrationTests;

public class ConsultationDeletionCleanupStoreTests
{
    [Fact]
    public async Task GetPlanAsync_returns_blob_refs_and_matching_clinical_knowledge_document_ids()
    {
        await using var context = CreateContext();
        var eventId = Guid.Parse("11111111-bbbb-cccc-dddd-eeeeeeeeeeee");
        context.Consultations.Add(new Consultation
        {
            Id = 10,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            SourceObjectReference = "private://consultations/10/audio",
            AudioBlobUri = "https://storage/audio/consultations/10/audio.wav",
            DeletedBy = "doctor-1",
            DeletedAtUtc = DateTime.UtcNow,
            Status = ConsultationStatus.Deleted
        });
        context.ConsultationDeletionCleanups.Add(new ConsultationDeletionCleanup
        {
            ConsultationId = 10,
            DeletionEventId = eventId,
            DeletedAtUtc = DateTime.UtcNow
        });
        context.ConsultationInboxMessages.Add(new ConsultationInboxMessage
        {
            ConsumerName = "backend-clinical-knowledge",
            EventId = Guid.NewGuid(),
            EventType = ConsultationIntegrationEvents.TranscriptReadyV1,
            EventVersion = 1,
            ReceivedAtUtc = DateTime.UtcNow,
            ClinicalKnowledgeDocumentId = "doctor-1#patient-1#10#2"
        });
        context.ConsultationInboxMessages.Add(new ConsultationInboxMessage
        {
            ConsumerName = "backend-clinical-knowledge",
            EventId = Guid.NewGuid(),
            EventType = ConsultationIntegrationEvents.TranscriptReadyV1,
            EventVersion = 1,
            ReceivedAtUtc = DateTime.UtcNow,
            ClinicalKnowledgeDocumentId = "doctor-1#patient-1#99#1"
        });
        await context.SaveChangesAsync();
        var store = new ConsultationDeletionCleanupStore(context);

        var plan = await store.GetPlanAsync(CreateEnvelope(eventId), CancellationToken.None);

        Assert.Equal(10, plan.ConsultationId);
        Assert.Equal("doctor-1", plan.RemovedBy);
        Assert.Contains("private://consultations/10/audio", plan.BlobObjectReferences);
        Assert.Contains("https://storage/audio/consultations/10/audio.wav", plan.BlobObjectReferences);
        Assert.Equal(["doctor-1#patient-1#10#2"], plan.ClinicalKnowledgeDocumentIds);
        Assert.Equal(ConsultationCleanupStatus.Pending, plan.BlobCleanupStatus);
        Assert.Equal(ConsultationCleanupStatus.Pending, plan.ClinicalKnowledgeCleanupStatus);
    }

    [Fact]
    public async Task Mark_methods_record_per_resource_completion()
    {
        await using var context = CreateContext();
        var eventId = Guid.Parse("22222222-bbbb-cccc-dddd-eeeeeeeeeeee");
        context.Consultations.Add(new Consultation
        {
            Id = 10,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            DeletedBy = "doctor-1",
            DeletedAtUtc = DateTime.UtcNow,
            Status = ConsultationStatus.Deleted
        });
        context.ConsultationDeletionCleanups.Add(new ConsultationDeletionCleanup
        {
            ConsultationId = 10,
            DeletionEventId = eventId,
            DeletedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var store = new ConsultationDeletionCleanupStore(context);

        await store.MarkBlobCleanupAsync(
            CreateEnvelope(eventId),
            ConsultationCleanupStatus.Completed,
            CancellationToken.None);
        await store.MarkClinicalKnowledgeCleanupAsync(
            CreateEnvelope(eventId),
            ConsultationCleanupStatus.NotRequired,
            CancellationToken.None);

        var cleanup = Assert.Single(context.ConsultationDeletionCleanups);
        Assert.Equal(ConsultationCleanupStatus.Completed, cleanup.BlobCleanupStatus);
        Assert.NotNull(cleanup.BlobCleanupCompletedAtUtc);
        Assert.Equal(ConsultationCleanupStatus.NotRequired, cleanup.ClinicalKnowledgeCleanupStatus);
        Assert.NotNull(cleanup.ClinicalKnowledgeCleanupCompletedAtUtc);
        Assert.Equal(2, cleanup.AttemptCount);
    }

    private static MedicalAssistantDatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MedicalAssistantDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new MedicalAssistantDatabaseContext(options, new HttpContextAccessor());
    }

    private static IntegrationEventEnvelope<ConsultationDeletedV1> CreateEnvelope(Guid eventId)
    {
        return new IntegrationEventEnvelope<ConsultationDeletedV1>(
            eventId,
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
