using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Services;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using Moq;

namespace MedicalAssistant.Application.UnitTests.Features;

public class ConsultationOutboxRelayTests
{
    [Fact]
    public async Task Relay_marks_outbox_message_published_only_after_confirmed_publish_returns()
    {
        var message = PendingMessage(10);
        var store = new Mock<IConsultationOutboxStore>();
        store
            .Setup(s => s.ClaimDueAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([message]);
        var publisher = new Mock<IConsultationOutboxPublisher>();
        var sequence = new MockSequence();
        publisher.InSequence(sequence)
            .Setup(p => p.PublishAsync(message, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        store.InSequence(sequence)
            .Setup(s => s.MarkPublishedAsync(message.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var relay = new ConsultationOutboxRelay(store.Object, publisher.Object);

        await relay.ProcessDueBatchAsync(CancellationToken.None);

        publisher.Verify(p => p.PublishAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.MarkPublishedAsync(message.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.MarkFailedAsync(
            It.IsAny<long>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Relay_schedules_backoff_when_confirmed_publish_fails()
    {
        var message = PendingMessage(11);
        var store = new Mock<IConsultationOutboxStore>();
        store
            .Setup(s => s.ClaimDueAsync(
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([message]);
        var publisher = new Mock<IConsultationOutboxPublisher>();
        publisher
            .Setup(p => p.PublishAsync(message, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));
        var relay = new ConsultationOutboxRelay(store.Object, publisher.Object);

        await relay.ProcessDueBatchAsync(CancellationToken.None);

        store.Verify(s => s.MarkPublishedAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(s => s.MarkFailedAsync(
            message.Id,
            "PublishFailed",
            "InvalidOperationException",
            It.Is<DateTime>(nextAttempt => nextAttempt > DateTime.UtcNow),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ConsultationOutboxMessage PendingMessage(long id) => new()
    {
        Id = id,
        EventId = Guid.NewGuid(),
        EventType = "consultation.audio-uploaded.v1",
        EventVersion = 1,
        OccurredAtUtc = DateTime.UtcNow,
        Producer = "medicalassistant.backend",
        Payload = "{}",
        CreatedAtUtc = DateTime.UtcNow,
        Status = ConsultationEventMessageStatus.Pending
    };
}
