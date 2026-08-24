using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Models;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Services;

public sealed class ConsultationOutboxRelay
{
    private readonly IConsultationOutboxStore _store;
    private readonly IConsultationOutboxPublisher _publisher;
    private readonly ConsultationOutboxRelayOptions _options;
    private readonly IConsultationOutboxRelayObserver? _observer;

    public ConsultationOutboxRelay(
        IConsultationOutboxStore store,
        IConsultationOutboxPublisher publisher,
        IOptions<ConsultationOutboxRelayOptions>? options = null,
        IConsultationOutboxRelayObserver? observer = null)
    {
        _store = store;
        _publisher = publisher;
        _options = options?.Value ?? new ConsultationOutboxRelayOptions();
        _observer = observer;
    }

    public async Task<int> ProcessDueBatchAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var messages = await _store.ClaimDueAsync(
            _options.BatchSize,
            _options.LeaseOwner,
            _options.LeaseDuration,
            now,
            cancellationToken);
        _observer?.BatchClaimed(messages.Count);

        foreach (var message in messages)
        {
            try
            {
                await _publisher.PublishAsync(message, cancellationToken);
                await _store.MarkPublishedAsync(message.Id, DateTime.UtcNow, cancellationToken);
                _observer?.MessagePublished(message.EventType);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await _store.MarkFailedAsync(
                    message.Id,
                    "PublishFailed",
                    ex.GetType().Name,
                    DateTime.UtcNow.Add(_options.FailureBackoff),
                    cancellationToken);
                _observer?.MessagePublishFailed(message.EventType, "PublishFailed");
            }
        }

        return messages.Count;
    }
}
