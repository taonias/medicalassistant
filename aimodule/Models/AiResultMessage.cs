namespace MedicalAssistant.AiModule.Models;

public static class AiResultEventTypes
{
    public const string ChatCompleted = "Chat.Completed";
    public const string StructuredDataCompleted = "StructuredData.Completed";
}

/// <summary>
/// Integration message published to <c>ai.results</c>. Must match MedicalAssistant.Api.
/// </summary>
public sealed class AiResultMessage
{
    public required string EventType { get; init; }
    public required string CorrelationId { get; init; }
    public string? Answer { get; init; }
    public List<string> Citations { get; init; } = [];
    public List<string> SuggestedActions { get; init; } = [];
    public string? ResponsePayload { get; init; }
    public string? FailureReason { get; init; }
    public int ConsultationId { get; init; }
    public int? TranscriptId { get; init; }
    public string? SchemaVersion { get; init; }
    public string? StructuredPayload { get; init; }
}
