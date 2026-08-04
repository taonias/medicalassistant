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
