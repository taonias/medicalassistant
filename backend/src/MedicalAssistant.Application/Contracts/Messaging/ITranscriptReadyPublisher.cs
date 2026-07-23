using MedicalAssistant.Application.Models.Messaging;

namespace MedicalAssistant.Application.Contracts.Messaging;

public interface ITranscriptReadyPublisher
{
    Task PublishAsync(TranscriptReadyMessage message, CancellationToken cancellationToken = default);
}
