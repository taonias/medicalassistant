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

    public ConsultationOutboxRelay(
        IConsultationOutboxStore store,
        IConsultationOutboxPublisher publisher,
        IOptions<ConsultationOutboxRelayOptions>? options = null)
    {
        _store = store;
        _publisher = publisher;
        _options = options?.Value ?? new ConsultationOutboxRelayOptions();
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

        foreach (var message in messages)
        {
            try
            {
                await _publisher.PublishAsync(message, cancellationToken);
                await _store.MarkPublishedAsync(message.Id, DateTime.UtcNow, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await _store.MarkFailedAsync(
                    message.Id,
                    "PublishFailed",
                    ex.GetType().Name,
                    DateTime.UtcNow.Add(_options.FailureBackoff),
                    cancellationToken);
            }
        }

        return messages.Count;
    }
}
