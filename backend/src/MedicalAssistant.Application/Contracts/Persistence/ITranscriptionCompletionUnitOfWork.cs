using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface ITranscriptionCompletionUnitOfWork
{
    Task<TranscriptionCompletionResult> CompleteAsync(
        TranscriptionCompletionRequest request,
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

public enum TranscriptionCompletionStatus
{
    Completed = 0,
    DuplicateCompleted = 1
}
