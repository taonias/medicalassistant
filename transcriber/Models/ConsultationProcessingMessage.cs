namespace MedicalAssistant.Transcriber.Models;

public static class ConsultationFileTypes
{
    public const string Audio = "Audio";
    public const string Document = "Document";
}

/// <summary>
/// Must match the JSON contract published by MedicalAssistant.Api.
/// </summary>
public sealed class ConsultationProcessingMessage
{
    public required string EventType { get; init; }
    public int ConsultationId { get; init; }
    public int? PatientId { get; init; }
    public required string DoctorId { get; init; }
    public required string Status { get; init; }
    public string? FileType { get; init; }
    public string? BlobUri { get; init; }
    public string? ContentType { get; init; }
    public string? FileName { get; init; }
    public int? DurationSeconds { get; init; }
    public required string CorrelationId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
}
