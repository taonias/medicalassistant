namespace MedicalAssistant.Transcriber.Models;

public static class TranscriptReadyEventTypes
{
    public const string Ready = "Transcript.Ready";
}

/// <summary>
/// Integration message for LLM workers that process a completed/edited transcript.
/// Must match the JSON contract published by MedicalAssistant.Api.
/// </summary>
public sealed class TranscriptReadyMessage
{
    public required string EventType { get; init; }
    public int TranscriptId { get; init; }
    public int ConsultationId { get; init; }
    public required string CorrelationId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
}
