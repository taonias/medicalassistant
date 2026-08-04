namespace MedicalAssistant.Application.Contracts.Storage;

public interface IConsultationBlobCleanupService
{
    Task DeleteIfExistsAsync(
        IReadOnlyCollection<string> objectReferences,
        CancellationToken cancellationToken = default);
}
