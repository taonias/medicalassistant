using MedicalAssistant.AiModule.Models;

namespace MedicalAssistant.AiModule.Services;

public interface IAiResultPublisher
{
    Task PublishAsync(AiResultMessage message, CancellationToken cancellationToken = default);
}
