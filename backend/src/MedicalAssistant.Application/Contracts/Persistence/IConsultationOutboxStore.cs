using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface IConsultationOutboxStore
{
    Task<IReadOnlyList<ConsultationOutboxMessage>> ClaimDueAsync(
        int batchSize,
        string leaseOwner,
        TimeSpan leaseDuration,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    Task MarkPublishedAsync(
        long messageId,
        DateTime publishedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        long messageId,
        string failureCategory,
        string failureCode,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken = default);
}
