namespace MedicalAssistant.Application.Contracts.Storage;

public interface IBlobStorageService
{
    Task<string> UploadAsync(string container, string blobName, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> DownloadAsync(string container, string blobName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string container, string blobName, CancellationToken cancellationToken = default);
    Task<string> GenerateSasUriAsync(string container, string blobName, TimeSpan expiry, CancellationToken cancellationToken = default);
}
