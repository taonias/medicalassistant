using MedicalAssistant.Application.Models.Messaging;

namespace MedicalAssistant.Application.Contracts.Messaging;

public interface IAiRequestPublisher
{
    Task PublishChatAsync(ChatRequestedMessage message, CancellationToken cancellationToken = default);
    Task PublishIndexAsync(IndexDocumentsRequestedMessage message, CancellationToken cancellationToken = default);
}
