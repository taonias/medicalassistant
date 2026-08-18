using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface ITranscriptReadyPreparationStore
{
    Task<TranscriptReadyPreparationResult> PrepareAsync(
        string consumerName,
        IntegrationEventEnvelope<ConsultationTranscriptReadyV1> envelope,
        CancellationToken cancellationToken = default);

    Task CompleteAcceptedAsync(
        string consumerName,
        IntegrationEventEnvelope<ConsultationTranscriptReadyV1> envelope,
        TranscriptReadyAcceptedResult accepted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that clinical-knowledge ingestion for a prepared transcript failed. The inbox
    /// row is marked Failed with the given code and the consultation gets a doctor-visible
    /// failure reason (its workflow status is left unchanged, because the transcript is still
    /// valid). No automatic retry occurs; the doctor retries manually from the UI.
    /// </summary>
    Task RecordFailedAsync(
        string consumerName,
        IntegrationEventEnvelope<ConsultationTranscriptReadyV1> envelope,
        string failureCode,
        CancellationToken cancellationToken = default);
}

public sealed record TranscriptReadyPreparationResult(
    TranscriptReadyPreparationStatus Status,
    PreparedSessionTranscriptRequest? Request);

public sealed record TranscriptReadyAcceptedResult(
    Guid IngestionId,
    string DocumentId,
    bool Duplicate);

public sealed record PreparedSessionTranscriptRequest(
    int ConsultationId,
    int TranscriptId,
    int TranscriptRevision,
    int PatientId,
    string? PatientExternalId,
    string? PatientDisplayName,
    string DoctorId,
    DateTime ConsultationDate,
    string? LanguageCode,
    string TranscriptText,
    string CorrelationId);

public enum TranscriptReadyPreparationStatus
{
    Prepared = 0,
    DuplicateCompleted = 1,
    IgnoredDeleted = 2,
    IgnoredSuperseded = 3,
    IgnoredUnauthorized = 4
}
