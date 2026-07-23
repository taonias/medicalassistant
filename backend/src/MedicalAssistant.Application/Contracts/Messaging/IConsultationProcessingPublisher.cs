using MedicalAssistant.Application.Models.Messaging;

namespace MedicalAssistant.Application.Contracts.Messaging;

public interface IConsultationProcessingPublisher
{
    Task PublishAsync(ConsultationProcessingMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes any still-queued processing messages for the consultation (best-effort).
    /// </summary>
    Task RemovePendingForConsultationAsync(int consultationId, CancellationToken cancellationToken = default);
}
