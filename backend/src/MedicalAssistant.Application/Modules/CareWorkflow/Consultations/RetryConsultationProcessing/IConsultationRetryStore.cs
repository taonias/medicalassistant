namespace MedicalAssistant.Application.Contracts.Persistence;

/// <summary>
/// Doctor-triggered manual retry of a failed consultation processing step. Automatic retries
/// are disabled, so a failed transcription or clinical-knowledge indexing is re-driven only
/// when the doctor asks for it. The store decides which step to re-run from the consultation's
/// current state and re-publishes the appropriate integration event through the outbox.
/// </summary>
public interface IConsultationRetryStore
{
    Task<ConsultationRetryOutcome> RequeueAsync(
        int consultationId,
        string doctorId,
        CancellationToken cancellationToken = default);
}

public sealed record ConsultationRetryOutcome(
    ConsultationRetryKind Kind,
    int ConsultationId);

public enum ConsultationRetryKind
{
    Transcription = 0,
    Indexing = 1
}
