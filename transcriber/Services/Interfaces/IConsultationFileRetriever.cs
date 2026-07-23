using MedicalAssistant.Transcriber.Models;

namespace MedicalAssistant.Transcriber.Services.Interfaces;

public interface IConsultationFileRetriever
{
    Task<RetrievedConsultationFile> RetrieveAsync(
        ConsultationProcessingMessage message,
        CancellationToken cancellationToken = default);
}
