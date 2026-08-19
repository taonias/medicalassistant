using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.EventHandlers;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace MedicalAssistant.Application.UnitTests;

public class ConsultationIngestionFailedIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_records_an_indexing_failure_for_the_consultation()
    {
        var store = new RecordingStore();
        var handler = new ConsultationIngestionFailedIntegrationEventHandler(
            store, NullLogger<ConsultationIngestionFailedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(CreateEnvelope("42", "Chunking failed"), CancellationToken.None);

        Assert.Equal(42, store.ConsultationId);
        Assert.False(store.Succeeded);
        Assert.Equal("Chunking failed", store.Reason);
    }

    [Fact]
    public async Task HandleAsync_ignores_a_non_numeric_session_id()
    {
        var store = new RecordingStore();
        var handler = new ConsultationIngestionFailedIntegrationEventHandler(
            store, NullLogger<ConsultationIngestionFailedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(CreateEnvelope("not-a-consultation", "boom"), CancellationToken.None);

        Assert.Null(store.ConsultationId);
    }

    private static IntegrationEventEnvelope<ConsultationIngestionFailedV1> CreateEnvelope(
        string sessionId, string reason) =>
        new(Guid.NewGuid(), ConsultationIntegrationEvents.IngestionFailedV1, 1, DateTime.UtcNow,
            "clinical-knowledge", null, null,
            new ConsultationIngestionFailedV1(sessionId, Guid.NewGuid(), reason));

    private sealed class RecordingStore : ITranscriptReadyPreparationStore
    {
        public int? ConsultationId { get; private set; }
        public bool Succeeded { get; private set; }
        public string? Reason { get; private set; }

        public Task RecordIngestionOutcomeAsync(
            int consultationId, bool succeeded, string? failureReason, CancellationToken cancellationToken = default)
        {
            ConsultationId = consultationId;
            Succeeded = succeeded;
            Reason = failureReason;
            return Task.CompletedTask;
        }

        public Task<TranscriptReadyPreparationResult> PrepareAsync(
            string consumerName,
            IntegrationEventEnvelope<ConsultationTranscriptReadyV1> envelope,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task CompleteAcceptedAsync(
            string consumerName,
            IntegrationEventEnvelope<ConsultationTranscriptReadyV1> envelope,
            TranscriptReadyAcceptedResult accepted,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task RecordFailedAsync(
            string consumerName,
            IntegrationEventEnvelope<ConsultationTranscriptReadyV1> envelope,
            string failureCode,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
