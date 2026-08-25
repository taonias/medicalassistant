using System.Text.Json.Serialization;

namespace MedicalAssistant.EventBus.Contracts;

public sealed record ConsultationAudioUploadedV1(
    [property: JsonPropertyName("consultationId")] int ConsultationId,
    [property: JsonPropertyName("fileId")] string FileId,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("storageObjectReference")] string StorageObjectReference,
    [property: JsonPropertyName("durationSeconds")] int? DurationSeconds);

public sealed record ConsultationDocumentUploadedV1(
    [property: JsonPropertyName("consultationId")] int ConsultationId,
    [property: JsonPropertyName("fileId")] string FileId,
    [property: JsonPropertyName("documentType")] string? DocumentType,
    [property: JsonPropertyName("contentType")] string ContentType,
    [property: JsonPropertyName("storageObjectReference")] string StorageObjectReference);

public sealed record ConsultationTranscriptReadyV1(
    [property: JsonPropertyName("consultationId")] int ConsultationId,
    [property: JsonPropertyName("fileId")] string FileId,
    [property: JsonPropertyName("transcriptId")] int TranscriptId,
    [property: JsonPropertyName("transcriptRevision")] int TranscriptRevision,
    [property: JsonPropertyName("languageCode")] string? LanguageCode);

public sealed record ConsultationTranscriptionFailedV1(
    [property: JsonPropertyName("consultationId")] int ConsultationId,
    [property: JsonPropertyName("fileId")] string FileId,
    [property: JsonPropertyName("failureCode")] string FailureCode,
    [property: JsonPropertyName("failureCategory")] string FailureCategory);

public sealed record ConsultationDeletedV1(
    [property: JsonPropertyName("consultationId")] int ConsultationId,
    [property: JsonPropertyName("deletedAtUtc")] DateTime DeletedAtUtc,
    [property: JsonPropertyName("reasonCode")] string? ReasonCode);

/// <summary>
/// A clinical-knowledge ingestion reached a terminal failure out of band, after the backend
/// already accepted it. Produced by the Clinical Knowledge service (a separate codebase), so
/// the wire shape here is the contract — keep the property names in sync on both sides.
/// <c>SessionId</c> is the backend consultation id as a string.
/// </summary>
public sealed record ConsultationIngestionFailedV1(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("ingestionId")] Guid IngestionId,
    [property: JsonPropertyName("reason")] string? Reason);
