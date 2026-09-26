namespace MedicalAssistant.AiModule.Models;

public static class AiRequestEventTypes
{
    public const string ChatRequested = "Chat.Requested";
}

/// <summary>
/// Discriminator-only envelope so inbound <c>ai.requests</c> JSON can be routed
/// without assuming a single payload shape.
/// </summary>
public sealed class AiRequestEnvelope
{
    public required string EventType { get; init; }
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Integration message published by MedicalAssistant.Api for chat.
/// </summary>
public sealed class ChatRequestedMessage
{
    public required string EventType { get; init; }
    public int ChatRequestId { get; init; }
    public required string DoctorId { get; init; }
    public int? PatientId { get; init; }
    public required string Message { get; init; }
    public required string ContextJson { get; init; }
    public string? SessionId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
}
