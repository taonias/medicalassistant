using System.Text.Json;

namespace MedicalAssistance.Ingestion.Api.Ingestions;

/// <summary>
/// A transactional-outbox row: an integration event written in the same transaction as the
/// state change it announces, so the fact and its publication cannot come apart. A relay
/// publishes it to the shared event bus and stamps <see cref="PublishedAt"/>. The stored
/// <see cref="Body"/> is the complete, ready-to-publish envelope — the relay never rebuilds it.
/// </summary>
public sealed class IntegrationEventOutboxMessage
{
    public Guid Id { get; set; }

    /// <summary>The envelope's globally-unique event id (idempotency key), used as the message id.</summary>
    public Guid EventId { get; set; }

    /// <summary>The routing key / event type, e.g. clinicalknowledge.ingestion-failed.v1.</summary>
    public required string EventType { get; set; }

    /// <summary>The complete integration-event envelope JSON, exactly as it goes on the wire.</summary>
    public required string Body { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }

    /// <summary>Routing key for the ingestion-failed contract shared with the backend.</summary>
    public const string IngestionFailedEventType = "clinicalknowledge.ingestion-failed.v1";

    private const string Producer = "clinical-knowledge";

    // Match the backend's IntegrationEventSerializer (System.Text.Json Web defaults): camelCase
    // property names, ISO-8601 dates, Guids as strings. The envelope field names and payload
    // shape are the cross-codebase contract — see ConsultationIngestionFailedV1 on the backend.
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Builds the outbox row for a transcript ingestion that reached a terminal failure.
    /// <paramref name="sessionId"/> is the backend consultation id.
    /// </summary>
    public static IntegrationEventOutboxMessage IngestionFailed(Guid ingestionId, string sessionId, string? reason)
    {
        var eventId = Guid.NewGuid();
        var envelope = new
        {
            eventId,
            eventType = IngestionFailedEventType,
            eventVersion = 1,
            occurredAtUtc = DateTime.UtcNow,
            producer = Producer,
            correlationId = (string?)null,
            causationId = (string?)null,
            payload = new
            {
                sessionId,
                ingestionId,
                reason
            }
        };

        return new IntegrationEventOutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventType = IngestionFailedEventType,
            Body = JsonSerializer.Serialize(envelope, WireJson),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
