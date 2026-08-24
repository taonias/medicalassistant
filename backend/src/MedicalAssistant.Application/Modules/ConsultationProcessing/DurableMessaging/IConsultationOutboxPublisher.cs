using MedicalAssistant.Domain;

namespace MedicalAssistant.Application.Contracts.Messaging;

public interface IConsultationOutboxPublisher
{
    Task PublishAsync(
        ConsultationOutboxMessage message,
        CancellationToken cancellationToken = default);
}
