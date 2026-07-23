using MedicalAssistant.Transcriber.Models;

namespace MedicalAssistant.Transcriber.Services.Interfaces;

public interface ISpeechTranscriptionService
{
    Task<string> TranscribeAsync(
        RetrievedConsultationFile file,
        CancellationToken cancellationToken = default);
}
