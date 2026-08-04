using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
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

        Assert.Equal(Application.Contracts.Persistence.TranscriptionInboxClaimStatus.Claimed, result.Status);
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

        Assert.Equal(Application.Contracts.Persistence.TranscriptionInboxClaimStatus.DuplicateCompleted, result.Status);
        Assert.Null(context.ConsultationInboxMessages.Single().LeaseOwner);
    }

    private static MedicalAssistantDatabaseContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MedicalAssistantDatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new MedicalAssistantDatabaseContext(options, new HttpContextAccessor());
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
