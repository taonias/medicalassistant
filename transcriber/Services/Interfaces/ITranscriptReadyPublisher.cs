using MedicalAssistant.Transcriber.Models;

namespace MedicalAssistant.Transcriber.Services.Interfaces;

public interface ITranscriptReadyPublisher
{
    Task PublishAsync(TranscriptReadyMessage message, CancellationToken cancellationToken = default);
}
