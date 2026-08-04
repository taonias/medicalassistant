using Azure;
using Azure.Storage.Blobs;
using MedicalAssistant.Application.Helpers;
using MedicalAssistant.Transcription.Worker.Options;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcription.Worker.Storage;

public sealed class AzurePrivateBlobObjectClient : IPrivateBlobObjectClient
{
    private readonly TranscriptionBlobRetrievalOptions _options;
    private readonly BlobServiceClient _blobServiceClient;

    public AzurePrivateBlobObjectClient(IOptions<TranscriptionBlobRetrievalOptions> options)
    {
        _options = options.Value;
        _blobServiceClient = new BlobServiceClient(_options.ConnectionString);
    }

    public async Task<PrivateBlobObject> OpenReadAsync(
        string storageObjectReference,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blobName = BlobUriHelper.ExtractBlobName(
                storageObjectReference,
                _options.ConsultationAudioContainer);
            var blobClient = _blobServiceClient
                .GetBlobContainerClient(_options.ConsultationAudioContainer)
                .GetBlobClient(blobName);

            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);

            return new PrivateBlobObject(
                download.Value.Content,
                properties.Value.ContentType,
                properties.Value.ContentLength);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new BlobRetrievalFailureException(
                BlobRetrievalFailureCategory.NotFound,
                "audio-not-found",
                "Audio object was not found.",
                ex);
        }
        catch (BlobRetrievalFailureException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BlobRetrievalFailureException(
                BlobRetrievalFailureCategory.Transient,
                "audio-read-failed",
                "Audio object could not be read.",
                ex);
        }
    }
}
