using MedicalAssistant.Application.Models.Messaging;

namespace MedicalAssistant.Application.Contracts.Messaging;

public interface IAiResultProcessor
{
    Task ProcessAsync(AiResultMessage message, CancellationToken cancellationToken = default);
}
