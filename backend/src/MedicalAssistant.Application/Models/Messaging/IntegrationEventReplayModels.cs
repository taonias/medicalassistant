using System.Text.Json;
using MedicalAssistant.EventBus;

namespace MedicalAssistant.Application.Models.Messaging;

public sealed record DeadLetteredIntegrationEvent(
    string SubscriberName,
    string EnvelopeJson,
    int AttemptCount,
    string? FailureCategory = null,
    DateTimeOffset? DeadLetteredAtUtc = null);

public sealed record IntegrationEventReplayCommand(
    DeadLetteredIntegrationEvent DeadLetteredEvent,
    string OperatorId,
    string Reason,
    string? ReplacementPayloadJson = null);

public sealed record SafeDeadLetteredIntegrationEventMetadata(
    Guid EventId,
    string EventType,
    int EventVersion,
    string SubscriberName,
    int AttemptCount,
    string? ConsultationId,
    string? CorrelationId,
    string? CausationId,
    string? FailureCategory,
    DateTimeOffset? DeadLetteredAtUtc);

public sealed record IntegrationEventReplayMessage(
    Guid EventId,
    string EventType,
    string? CorrelationId,
    string EnvelopeJson);

public sealed record IntegrationEventReplayResult(
    bool Replayed,
    string? FailureCode,
    SafeDeadLetteredIntegrationEventMetadata Metadata,
    string AuditAction,
    string AuditDetails);

public sealed record IntegrationEventReplaySafetyDecision(
    bool IsSafe,
    string? FailureCode)
{
    public static IntegrationEventReplaySafetyDecision Safe() => new(true, null);

    public static IntegrationEventReplaySafetyDecision Unsafe(string failureCode) => new(false, failureCode);
}

public static class DeadLetteredIntegrationEventInspector
{
    public static SafeDeadLetteredIntegrationEventMetadata Inspect(DeadLetteredIntegrationEvent deadLetteredEvent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deadLetteredEvent.SubscriberName);
        ArgumentException.ThrowIfNullOrWhiteSpace(deadLetteredEvent.EnvelopeJson);

        using var document = JsonDocument.Parse(deadLetteredEvent.EnvelopeJson);
        var root = document.RootElement;

        return new SafeDeadLetteredIntegrationEventMetadata(
            EventId: ReadGuid(root, "eventId"),
            EventType: ReadRequiredString(root, "eventType"),
            EventVersion: ReadRequiredInt(root, "eventVersion"),
            SubscriberName: deadLetteredEvent.SubscriberName.Trim(),
            AttemptCount: Math.Max(0, deadLetteredEvent.AttemptCount),
            ConsultationId: ReadPayloadIdentifier(root, "consultationId"),
            CorrelationId: ReadOptionalString(root, "correlationId"),
            CausationId: ReadOptionalString(root, "causationId"),
            FailureCategory: string.IsNullOrWhiteSpace(deadLetteredEvent.FailureCategory)
                ? null
                : deadLetteredEvent.FailureCategory.Trim(),
            DeadLetteredAtUtc: deadLetteredEvent.DeadLetteredAtUtc);
    }

    private static Guid ReadGuid(JsonElement root, string propertyName)
    {
        var value = ReadRequiredString(root, propertyName);
        return Guid.TryParse(value, out var parsed)
            ? parsed
            : throw new JsonException($"Integration event property '{propertyName}' must be a valid GUID.");
    }

    private static int ReadRequiredInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException($"Integration event property '{propertyName}' is required.");
        }

        return value.GetInt32();
    }

    private static string ReadRequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Integration event property '{propertyName}' is required.");
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text)
            ? throw new JsonException($"Integration event property '{propertyName}' is required.")
            : text;
    }

    private static string? ReadOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Integration event property '{propertyName}' must be a string when provided.");
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? ReadPayloadIdentifier(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        return value.ValueKind == JsonValueKind.Number ? value.GetRawText() : null;
    }
}
