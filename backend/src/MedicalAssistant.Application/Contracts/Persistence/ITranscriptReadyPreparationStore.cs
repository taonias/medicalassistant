using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface ITranscriptReadyPreparationStore
{
    Task<TranscriptReadyPreparationResult> PrepareAsync(
        string consumerName,
        IntegrationEventEnvelope<ConsultationTranscriptReadyV1> envelope,
        CancellationToken cancellationToken = default);
}

public sealed record TranscriptReadyPreparationResult(
    TranscriptReadyPreparationStatus Status,
    PreparedSessionTranscriptRequest? Request);

public sealed record PreparedSessionTranscriptRequest(
    int ConsultationId,
    int TranscriptId,
    int TranscriptRevision,
    int? PatientId,
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
