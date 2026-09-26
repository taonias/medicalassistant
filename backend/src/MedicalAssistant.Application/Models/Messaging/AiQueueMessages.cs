namespace MedicalAssistant.Application.Models.Messaging;

public static class AiRequestEventTypes
{
    public const string ChatRequested = "Chat.Requested";
    public const string DocumentsIndexRequested = "Documents.IndexRequested";
}

public static class AiResultEventTypes
{
    public const string ChatCompleted = "Chat.Completed";
    public const string ChatFailed = "Chat.Failed";
    public const string ActionCompleted = "Action.Completed";
    public const string ActionFailed = "Action.Failed";
    public const string StructuredDataCompleted = "StructuredData.Completed";
    public const string StructuredDataFailed = "StructuredData.Failed";
    public const string DocumentsIndexCompleted = "Documents.IndexCompleted";
    public const string DocumentsIndexFailed = "Documents.IndexFailed";
}

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

public sealed class IndexDocumentMessage
{
    public required string Id { get; init; }
    public required int PatientId { get; init; }
    public int? ConsultationId { get; init; }
    public required string DocType { get; init; }
    public required string Content { get; init; }
}

public sealed class IndexDocumentsRequestedMessage
{
    public required string EventType { get; init; }
    public required string CorrelationId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public List<IndexDocumentMessage> Documents { get; init; } = [];
}

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
