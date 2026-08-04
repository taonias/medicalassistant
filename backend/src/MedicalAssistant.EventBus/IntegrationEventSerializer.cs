using System.Text.Json;

namespace MedicalAssistant.EventBus;

public static class IntegrationEventSerializer
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false
    };

    public static string Serialize<TPayload>(IntegrationEventEnvelope<TPayload> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return JsonSerializer.Serialize(envelope, Options);
    }

    public static object Deserialize(string json, IntegrationEventContractDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ArgumentNullException.ThrowIfNull(descriptor);

        var envelopeType = typeof(IntegrationEventEnvelope<>).MakeGenericType(descriptor.ClrType);
        return JsonSerializer.Deserialize(json, envelopeType, Options)
            ?? throw new IntegrationEventDispatchException(
                $"Integration event '{descriptor.EventType}' version {descriptor.EventVersion} could not be deserialized.");
    }
}
