using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MedicalAssistance.Ingestion.Api.Ingestions;

/// <summary>
/// Drains the integration-event outbox to the shared event bus. Polls for unpublished rows,
/// publishes each with publisher confirms, and stamps it published only after the broker
/// acknowledges — so an event written in a status transaction is delivered at-least-once even
/// if the broker was briefly unreachable when it was written. A failed publish is retried with
/// a bounded backoff; the row is never dropped. Inert unless a broker is configured.
///
/// Across a multi-instance fleet, a single-key advisory lock lets one instance drain a pass
/// while the others skip it, so no row is ever published twice. The lock is held on its own
/// connection for the length of the pass; a process that dies releases it when its connection
/// drops, so the next poll — this instance's or another's — is free to take over.
/// </summary>
public sealed class IntegrationEventOutboxRelay(
    IServiceScopeFactory scopeFactory,
    RabbitMqEventPublisher publisher,
    NpgsqlDataSource dataSource,
    IOptions<RabbitMqPublishOptions> options,
    ILogger<IntegrationEventOutboxRelay> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Integration-event outbox relay is disabled (no broker configured).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishDueAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Outbox relay pass failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PublishDueAsync(CancellationToken ct)
    {
        // One instance drains at a time. The lock lives on its own connection, held for the
        // whole pass and released when it disposes; another instance holding it means it is
        // already draining, so skip rather than double-publish.
        await using var lockConnection = await dataSource.OpenConnectionAsync(ct);
        await using var relayLock = await PostgresAdvisoryLock.TryAcquireAsync(
            lockConnection, PostgresAdvisoryLock.OutboxRelayKey, ct);
        if (relayLock is null)
            return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IngestionDbContext>();

        var now = DateTimeOffset.UtcNow;
        var due = await db.IntegrationEventOutbox
            .Where(m => m.PublishedAt == null && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in due)
        {
            try
            {
                await publisher.PublishAsync(
                    message.EventType, message.EventId.ToString(), Encoding.UTF8.GetBytes(message.Body), ct);
                message.PublishedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.Attempts++;
                message.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, 5 * message.Attempts));
                message.LastError = exception.Message;
                logger.LogWarning(
                    exception, "Failed to publish outbox event {EventId}; attempt {Attempts}",
                    message.EventId, message.Attempts);
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
