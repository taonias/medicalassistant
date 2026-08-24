using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Persistence.DatabaseContext;
using MedicalAssistant.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MedicalAssistant.Persistence.IntegrationTests;

public class TranscriptionInboxStoreTests
{
    [Fact]
    public async Task ClaimAsync_reclaims_expired_in_progress_event()
    {
        await using var context = CreateContext();
        var eventId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        AddCurrentAudioConsultation(context);
        context.ConsultationInboxMessages.Add(new ConsultationInboxMessage
        {
            ConsumerName = "transcription-worker",
            EventId = eventId,
            EventType = ConsultationIntegrationEvents.AudioUploadedV1,
            EventVersion = 1,
            ReceivedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            Status = ConsultationEventMessageStatus.InProgress,
            AttemptCount = 1,
            LeaseOwner = "dead-worker",
            LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
        });
        await context.SaveChangesAsync();
        var store = new TranscriptionInboxStore(context);

        var result = await store.ClaimAsync(
            "transcription-worker",
            CreateEnvelope(eventId),
            "new-worker",
            TimeSpan.FromMinutes(10),
            CancellationToken.None);

        Assert.Equal(TranscriptionInboxClaimStatus.Claimed, result.Status);
        var inbox = Assert.Single(context.ConsultationInboxMessages);
        Assert.Equal("new-worker", inbox.LeaseOwner);
        Assert.Equal(2, inbox.AttemptCount);
        Assert.True(inbox.LeaseExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task ClaimAsync_returns_duplicate_completed_without_reclaiming()
    {
        await using var context = CreateContext();
        var eventId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        AddCurrentAudioConsultation(context);
        context.ConsultationInboxMessages.Add(new ConsultationInboxMessage
        {
            ConsumerName = "transcription-worker",
            EventId = eventId,
            EventType = ConsultationIntegrationEvents.AudioUploadedV1,
            EventVersion = 1,
            ReceivedAtUtc = DateTime.UtcNow,
            Status = ConsultationEventMessageStatus.Completed,
            CompletedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var store = new TranscriptionInboxStore(context);

        var result = await store.ClaimAsync(
            "transcription-worker",
            CreateEnvelope(eventId),
            "new-worker",
            TimeSpan.FromMinutes(10),
            CancellationToken.None);

        Assert.Equal(TranscriptionInboxClaimStatus.DuplicateCompleted, result.Status);
        Assert.Null(context.ConsultationInboxMessages.Single().LeaseOwner);
    }

    [Fact]
    public async Task ClaimAsync_completes_and_skips_deleted_consultation_before_expensive_work()
    {
        await using var context = CreateContext();
        var consultation = AddCurrentAudioConsultation(context);
        consultation.MarkDeleted("doctor-1", "doctor-delete");
        await context.SaveChangesAsync();
        var store = new TranscriptionInboxStore(context);

        var result = await store.ClaimAsync(
            "transcription-worker",
            CreateEnvelope(Guid.Parse("77777777-7777-7777-7777-777777777777")),
            "worker-1",
            TimeSpan.FromMinutes(10),
            CancellationToken.None);

        Assert.Equal(TranscriptionInboxClaimStatus.SkippedDeleted, result.Status);
        var inbox = Assert.Single(context.ConsultationInboxMessages);
        Assert.Equal(ConsultationEventMessageStatus.Completed, inbox.Status);
        Assert.Equal("StateGate", inbox.LastFailureCategory);
        Assert.Equal("consultation-deleted", inbox.LastFailureCode);
        Assert.Null(inbox.LeaseOwner);
    }

    [Fact]
    public async Task ClaimAsync_completes_and_skips_superseded_source_before_expensive_work()
    {
        await using var context = CreateContext();
        var consultation = AddCurrentAudioConsultation(context);
        consultation.SourceObjectReference = "private://consultations/42/new-audio.wav";
        await context.SaveChangesAsync();
        var store = new TranscriptionInboxStore(context);

        var result = await store.ClaimAsync(
            "transcription-worker",
            CreateEnvelope(Guid.Parse("88888888-8888-8888-8888-888888888888")),
            "worker-1",
            TimeSpan.FromMinutes(10),
            CancellationToken.None);

        Assert.Equal(TranscriptionInboxClaimStatus.SkippedSuperseded, result.Status);
        var inbox = Assert.Single(context.ConsultationInboxMessages);
        Assert.Equal(ConsultationEventMessageStatus.Completed, inbox.Status);
        Assert.Equal("StateGate", inbox.LastFailureCategory);
        Assert.Equal("consultation-source-superseded", inbox.LastFailureCode);
        Assert.Null(inbox.LeaseOwner);
    }

    private static MedicalAssistantDatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MedicalAssistantDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new MedicalAssistantDatabaseContext(options, new HttpContextAccessor());
    }

    private static Consultation AddCurrentAudioConsultation(MedicalAssistantDatabaseContext context)
    {
        var consultation = new Consultation
        {
            Id = 42,
            DoctorId = "doctor-1",
            ConsultationDate = DateTime.UtcNow,
            SourceObjectReference = "private://consultations/42/audio.wav",
            Status = ConsultationStatus.AudioUploaded
        };
        context.Consultations.Add(consultation);
        return consultation;
    }

    private static IntegrationEventEnvelope<ConsultationAudioUploadedV1> CreateEnvelope(Guid eventId)
    {
        return new IntegrationEventEnvelope<ConsultationAudioUploadedV1>(
            eventId,
            ConsultationIntegrationEvents.AudioUploadedV1,
            1,
            DateTime.UtcNow,
            "medicalassistant.backend",
            "correlation-1",
            null,
            new ConsultationAudioUploadedV1(
                42,
                "file-42",
                "audio/wav",
                "private://consultations/42/audio.wav",
                3));
    }
}
