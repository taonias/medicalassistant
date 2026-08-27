using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.Persistence.DatabaseContext;
using MedicalAssistant.Persistence.Modules.ConsultationProcessing.TranscriptIngestion;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MedicalAssistant.Persistence.IntegrationTests;

public class TranscriptReadyPreparationStoreTests
{
    [Fact]
    public async Task PrepareAsync_loads_current_transcript_context_and_keeps_inbox_in_progress_until_acceptance()
    {
        await using var context = CreateContext();
        SeedPreparedConsultation(context);
        await context.SaveChangesAsync();
        var store = new TranscriptReadyPreparationStore(context);
        var envelope = CreateEnvelope(Guid.Parse("11111111-aaaa-aaaa-aaaa-111111111111"), revision: 2);

        var result = await store.PrepareAsync("backend-clinical-knowledge", envelope, CancellationToken.None);

        Assert.Equal(TranscriptReadyPreparationStatus.Prepared, result.Status);
        Assert.NotNull(result.Request);
        Assert.Equal(10, result.Request.ConsultationId);
        Assert.Equal(100, result.Request.TranscriptId);
        Assert.Equal(2, result.Request.TranscriptRevision);
        Assert.Equal(5, result.Request.PatientId);
        Assert.Equal("patient-ext-5", result.Request.PatientExternalId);
        Assert.Equal("Ada Lovelace", result.Request.PatientDisplayName);
        Assert.Equal("doctor-1", result.Request.DoctorId);
        Assert.Equal("el-GR", result.Request.LanguageCode);
        Assert.Equal("clinical transcript text", result.Request.TranscriptText);
        Assert.Equal("correlation-1", result.Request.CorrelationId);
        Assert.Equal(ConsultationStatus.Transcribed, context.Consultations.Single().Status);

        var inbox = Assert.Single(context.ConsultationInboxMessages);
        Assert.Equal(ConsultationEventMessageStatus.InProgress, inbox.Status);
        Assert.Null(inbox.ClinicalKnowledgeIngestionId);
        Assert.Null(inbox.ClinicalKnowledgeDocumentId);
        Assert.Null(inbox.LastFailureCategory);
        Assert.Null(inbox.LastFailureCode);
    }

    [Fact]
    public async Task CompleteAcceptedAsync_marks_inbox_completed_with_ingestion_and_document_identity()
    {
        await using var context = CreateContext();
        SeedPreparedConsultation(context);
        await context.SaveChangesAsync();
        var store = new TranscriptReadyPreparationStore(context);
        var envelope = CreateEnvelope(Guid.Parse("55555555-aaaa-aaaa-aaaa-555555555555"), revision: 2);
        await store.PrepareAsync("backend-clinical-knowledge", envelope, CancellationToken.None);

        await store.CompleteAcceptedAsync(
            "backend-clinical-knowledge",
            envelope,
            new TranscriptReadyAcceptedResult(
                Guid.Parse("aaaaaaaa-2222-2222-2222-aaaaaaaaaaaa"),
                "doctor-1#patient-ext-5#10#2",
                Duplicate: false),
            CancellationToken.None);

        var inbox = Assert.Single(context.ConsultationInboxMessages);
        Assert.Equal(ConsultationEventMessageStatus.Completed, inbox.Status);
        Assert.Equal(Guid.Parse("aaaaaaaa-2222-2222-2222-aaaaaaaaaaaa"), inbox.ClinicalKnowledgeIngestionId);
        Assert.Equal("doctor-1#patient-ext-5#10#2", inbox.ClinicalKnowledgeDocumentId);
        Assert.False(inbox.ClinicalKnowledgeDuplicate);
        Assert.Null(inbox.LastFailureCategory);
        Assert.Null(inbox.LastFailureCode);
        Assert.Equal(ConsultationStatus.StructuredDataPending, context.Consultations.Single().Status);
    }

    [Fact]
    public async Task PrepareAsync_treats_completed_inbox_event_as_duplicate_noop()
    {
        await using var context = CreateContext();
        SeedPreparedConsultation(context);
        var eventId = Guid.Parse("22222222-aaaa-aaaa-aaaa-222222222222");
        context.ConsultationInboxMessages.Add(new ConsultationInboxMessage
        {
            ConsumerName = "backend-clinical-knowledge",
            EventId = eventId,
            EventType = ConsultationIntegrationEvents.TranscriptReadyV1,
            EventVersion = 1,
            ReceivedAtUtc = DateTime.UtcNow,
            Status = ConsultationEventMessageStatus.Completed,
            CompletedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var store = new TranscriptReadyPreparationStore(context);

        var result = await store.PrepareAsync(
            "backend-clinical-knowledge",
            CreateEnvelope(eventId, revision: 2),
            CancellationToken.None);

        Assert.Equal(TranscriptReadyPreparationStatus.DuplicateCompleted, result.Status);
        Assert.Null(result.Request);
        Assert.Single(context.ConsultationInboxMessages);
    }

    [Fact]
    public async Task PrepareAsync_ignores_deleted_consultation_without_returning_transcript_text()
    {
        await using var context = CreateContext();
        var consultation = SeedPreparedConsultation(context);
        consultation.MarkDeleted("doctor-1", "doctor-delete");
        await context.SaveChangesAsync();
        var store = new TranscriptReadyPreparationStore(context);

        var result = await store.PrepareAsync(
            "backend-clinical-knowledge",
            CreateEnvelope(Guid.Parse("33333333-aaaa-aaaa-aaaa-333333333333"), revision: 2),
            CancellationToken.None);

        Assert.Equal(TranscriptReadyPreparationStatus.IgnoredDeleted, result.Status);
        Assert.Null(result.Request);
        var inbox = Assert.Single(context.ConsultationInboxMessages);
        Assert.Equal("TranscriptReadyStateGate", inbox.LastFailureCategory);
        Assert.Equal("consultation-deleted", inbox.LastFailureCode);
        Assert.Equal(ConsultationStatus.Deleted, context.Consultations.Single().Status);
    }

    [Fact]
    public async Task PrepareAsync_ignores_superseded_transcript_revision_without_returning_transcript_text()
    {
        await using var context = CreateContext();
        SeedPreparedConsultation(context);
        await context.SaveChangesAsync();
        var store = new TranscriptReadyPreparationStore(context);

        var result = await store.PrepareAsync(
            "backend-clinical-knowledge",
            CreateEnvelope(Guid.Parse("44444444-aaaa-aaaa-aaaa-444444444444"), revision: 1),
            CancellationToken.None);

        Assert.Equal(TranscriptReadyPreparationStatus.IgnoredSuperseded, result.Status);
        Assert.Null(result.Request);
        var inbox = Assert.Single(context.ConsultationInboxMessages);
        Assert.Equal("TranscriptReadyStateGate", inbox.LastFailureCategory);
        Assert.Equal("transcript-revision-superseded", inbox.LastFailureCode);
        Assert.Equal(ConsultationStatus.Transcribed, context.Consultations.Single().Status);
    }

    private static MedicalAssistantDatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MedicalAssistantDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new MedicalAssistantDatabaseContext(options, new HttpContextAccessor());
    }

    private static Consultation SeedPreparedConsultation(MedicalAssistantDatabaseContext context)
    {
        var patient = new Patient
        {
            Id = 5,
            FirstName = "Ada",
            LastName = "Lovelace",
            ExternalPatientId = "patient-ext-5",
            AssignedDoctorId = "doctor-1"
        };
        var consultation = new Consultation
        {
            Id = 10,
            PatientId = patient.Id,
            DoctorId = "doctor-1",
            ConsultationDate = new DateTime(2026, 8, 4, 10, 30, 0, DateTimeKind.Utc),
            SourceObjectReference = "private://consultations/10/audio",
            Status = ConsultationStatus.Transcribed
        };
        var transcript = new Transcript
        {
            Id = 100,
            ConsultationId = consultation.Id,
            Status = TranscriptStatus.Completed,
            TranscriptText = "clinical transcript text",
            Revision = 2
        };

        context.Patients.Add(patient);
        context.Consultations.Add(consultation);
        context.Transcripts.Add(transcript);
        return consultation;
    }

    private static IntegrationEventEnvelope<ConsultationTranscriptReadyV1> CreateEnvelope(
        Guid eventId,
        int revision)
    {
        return new IntegrationEventEnvelope<ConsultationTranscriptReadyV1>(
            eventId,
            ConsultationIntegrationEvents.TranscriptReadyV1,
            1,
            DateTime.UtcNow,
            "medicalassistant.backend",
            "correlation-1",
            null,
            new ConsultationTranscriptReadyV1(
                10,
                "private://consultations/10/audio",
                100,
                revision,
                "el-GR"));
    }
}
