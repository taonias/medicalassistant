using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface ITranscriptionCompletionUnitOfWork
{
    Task<TranscriptionCompletionResult> CompleteAsync(
        TranscriptionCompletionRequest request,
        CancellationToken cancellationToken = default);

    Task<TranscriptionFailureResult> FailAsync(
        TranscriptionFailureRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record TranscriptionCompletionRequest(
    string ConsumerName,
    IntegrationEventEnvelope<ConsultationAudioUploadedV1> Envelope,
    string TranscriptText,
    string? ExternalJobId,
    string? LanguageCode);

public sealed record TranscriptionCompletionResult(
    TranscriptionCompletionStatus Status,
    int ConsultationId,
    int? TranscriptId);

public sealed record TranscriptionFailureRequest(
    string ConsumerName,
    IntegrationEventEnvelope<ConsultationAudioUploadedV1> Envelope,
    string FailureCode,
    string FailureCategory);

public sealed record TranscriptionFailureResult(
    TranscriptionFailureStatus Status,
    int ConsultationId);

public enum TranscriptionCompletionStatus
{
    Completed = 0,
    DuplicateCompleted = 1
}

public enum TranscriptionFailureStatus
{
    Failed = 0,
    DuplicateCompleted = 1
}
