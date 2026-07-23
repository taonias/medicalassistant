using MedicalAssistant.Transcriber.Models;

namespace MedicalAssistant.Transcriber.Services.Interfaces;

public interface ITranscriptService
{
    Task CreateFromRetrievedFileAsync(
        ConsultationProcessingMessage message,
        RetrievedConsultationFile file,
        CancellationToken cancellationToken = default);
}
