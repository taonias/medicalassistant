using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.Persistence.Modules.ConsultationProcessing.DurableMessaging;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
public sealed class PostgreSqlDurableMessagingCharacterizationTests
{
    private readonly PostgreSqlDatabaseFixture _database;

    public PostgreSqlDurableMessagingCharacterizationTests(PostgreSqlDatabaseFixture database)
    {
        _database = database;
    }

    [Fact]
    public async Task Inbox_identity_is_unique_per_consumer_and_event()
    {
        await _database.ResetAsync();
        var eventId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        await using (var firstContext = _database.CreateContext())
        {
            firstContext.ConsultationInboxMessages.Add(CreateInbox(eventId));
            await firstContext.SaveChangesAsync();
        }

        await using (var duplicateContext = _database.CreateContext())
        {
            duplicateContext.ConsultationInboxMessages.Add(CreateInbox(eventId));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
        }

        await using var verification = _database.CreateContext();
        var inbox = await verification.ConsultationInboxMessages.SingleAsync();
        Assert.Equal("transcription-worker", inbox.ConsumerName);
        Assert.Equal(eventId, inbox.EventId);
    }

    [Fact]
    public async Task Concurrent_outbox_claimers_receive_disjoint_due_messages()
    {
        await _database.ResetAsync();
        var now = new DateTime(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc);
        await using (var setup = _database.CreateContext())
        {
            setup.ConsultationOutboxMessages.AddRange(
                Enumerable.Range(0, 4).Select(index => CreateOutbox(index, now)));
            await setup.SaveChangesAsync();
        }

        await using var firstContext = _database.CreateContext();
        await using var secondContext = _database.CreateContext();
        var firstStore = new ConsultationOutboxStore(firstContext);
        var secondStore = new ConsultationOutboxStore(secondContext);

        var claims = await Task.WhenAll(
            firstStore.ClaimDueAsync(2, "relay-a", TimeSpan.FromMinutes(5), now),
            secondStore.ClaimDueAsync(2, "relay-b", TimeSpan.FromMinutes(5), now));

        var firstIds = claims[0].Select(message => message.Id).ToHashSet();
        var secondIds = claims[1].Select(message => message.Id).ToHashSet();
        Assert.Equal(2, firstIds.Count);
        Assert.Equal(2, secondIds.Count);
        Assert.Empty(firstIds.Intersect(secondIds));

        await using var verification = _database.CreateContext();
        var persistedClaims = await verification.ConsultationOutboxMessages
            .OrderBy(message => message.Id)
            .ToListAsync();
        Assert.All(persistedClaims, message =>
            Assert.Equal(ConsultationEventMessageStatus.InProgress, message.Status));
        Assert.Equal(2, persistedClaims.Count(message => message.LeaseOwner == "relay-a"));
        Assert.Equal(2, persistedClaims.Count(message => message.LeaseOwner == "relay-b"));
    }

    private static ConsultationInboxMessage CreateInbox(Guid eventId)
    {
        return new ConsultationInboxMessage
        {
            ConsumerName = "transcription-worker",
            EventId = eventId,
            EventType = ConsultationIntegrationEvents.AudioUploadedV1,
            EventVersion = 1,
            ReceivedAtUtc = new DateTime(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc)
        };
    }

    private static ConsultationOutboxMessage CreateOutbox(
        int index,
        DateTime now)
    {
        return new ConsultationOutboxMessage
        {
            EventId = Guid.Parse($"cccccccc-cccc-cccc-cccc-{index + 1:000000000000}"),
            EventType = ConsultationIntegrationEvents.AudioUploadedV1,
            EventVersion = 1,
            OccurredAtUtc = now.AddMinutes(-10),
            Producer = "medicalassistant.backend",
            CorrelationId = $"claim-{index}",
            AggregateType = nameof(Consultation),
            AggregateId = (index + 1).ToString(),
            Payload = "{}",
            Status = ConsultationEventMessageStatus.Pending,
            CreatedAtUtc = now.AddSeconds(index - 10)
        };
    }
}
