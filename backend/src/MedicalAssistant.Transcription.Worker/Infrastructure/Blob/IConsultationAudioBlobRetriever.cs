namespace MedicalAssistant.Transcription.Worker.Storage;

public interface IConsultationAudioBlobRetriever
{
    Task<ConsultationAudioBlob> OpenReadAsync(
        string storageObjectReference,
        CancellationToken cancellationToken = default);
}

public sealed record ConsultationAudioBlob(
    Stream Content,
    string ContentType,
    long ContentLength);
