namespace MedicalAssistant.Application.Models.AiModule;

public class TranscriptionJobRequest
{
    public int ConsultationId { get; set; }
    public required string AudioBlobUri { get; set; }
    public required string CallbackUrl { get; set; }
    public required string CorrelationId { get; set; }
}

public class TranscriptionJobResponse
{
    public required string JobId { get; set; }
}

public class TranscriptionStatusResponse
{
    public required string JobId { get; set; }
    public required string Status { get; set; }
    public string? RawText { get; set; }
    public string? FailureReason { get; set; }
}

public class StructuredDataJobRequest
{
    public int ConsultationId { get; set; }
    public required string TranscriptText { get; set; }
    public string SchemaVersion { get; set; } = "v1";
    public required string CallbackUrl { get; set; }
    public required string CorrelationId { get; set; }
}

public class StructuredDataJobResponse
{
    public required string JobId { get; set; }
}

public class ChatRequest
{
    public required string Message { get; set; }
    public required string ContextJson { get; set; }
    public string? SessionId { get; set; }
}

public class ChatResponse
{
    public required string Answer { get; set; }
    public List<string> Citations { get; set; } = [];
    public List<string> SuggestedActions { get; set; } = [];
}

public class ActionJobRequest
{
    public required string ActionType { get; set; }
    public int? PatientId { get; set; }
    public int? ConsultationId { get; set; }
    public required string CorrelationId { get; set; }
    public required string CallbackUrl { get; set; }
    public string? ParametersJson { get; set; }
}

public class ActionJobResponse
{
    public required string JobId { get; set; }
}

public class ActionStatusResponse
{
    public required string JobId { get; set; }
    public required string Status { get; set; }
    public string? ResponsePayload { get; set; }
    public string? FailureReason { get; set; }
}

public class IndexDocument
{
    public required string Id { get; set; }
    public required int PatientId { get; set; }
    public int? ConsultationId { get; set; }
    public required string DocType { get; set; }
    public required string Content { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class IndexDocumentsJobRequest
{
    public List<IndexDocument> Documents { get; set; } = [];
}

public class IndexDocumentsResponse
{
    public required int IndexedCount { get; set; }
    public int FailedCount { get; set; }
}
