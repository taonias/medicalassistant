namespace MedicalAssistant.Transcription.Worker.Storage;

public interface IPrivateBlobObjectClient
{
    Task<PrivateBlobObject> OpenReadAsync(
        string storageObjectReference,
        CancellationToken cancellationToken = default);
}

public sealed record PrivateBlobObject(
    Stream Content,
    string ContentType,
    long ContentLength);
