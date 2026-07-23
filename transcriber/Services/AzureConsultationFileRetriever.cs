using Azure.Storage.Blobs;
using MedicalAssistant.Transcriber.Models;
using MedicalAssistant.Transcriber.Options;
using MedicalAssistant.Transcriber.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcriber.Services;

public sealed class AzureConsultationFileRetriever : IConsultationFileRetriever
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobStorageOptions _options;

    public AzureConsultationFileRetriever(IOptions<BlobStorageOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException("BlobStorage:ConnectionString is not configured.");

        _blobServiceClient = new BlobServiceClient(_options.ConnectionString);
    }

    public async Task<RetrievedConsultationFile> RetrieveAsync(
        ConsultationProcessingMessage message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.BlobUri))
            throw new InvalidOperationException(
                $"Consultation {message.ConsultationId} message has no blobUri.");

        var blobUri = new Uri(message.BlobUri);
        var builder = new BlobUriBuilder(blobUri);
        var container = builder.BlobContainerName;
        var blobName = builder.BlobName;

        if (string.IsNullOrWhiteSpace(container) || string.IsNullOrWhiteSpace(blobName))
            throw new InvalidOperationException($"Invalid blob URI: {message.BlobUri}");

        var blobClient = _blobServiceClient
            .GetBlobContainerClient(container)
            .GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(cancellationToken))
            throw new FileNotFoundException($"Blob not found: {container}/{blobName}");

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        await using var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        // Fully read so the function validates the content is reachable before auditing / speech.
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var content = buffer.ToArray();

        var fileType = string.IsNullOrWhiteSpace(message.FileType)
            ? InferFileType(container)
            : message.FileType;

        return new RetrievedConsultationFile
        {
            BlobUri = message.BlobUri,
            Container = container,
            BlobName = blobName,
            ContentType = message.ContentType
                ?? properties.Value.ContentType
                ?? "application/octet-stream",
            ByteLength = content.LongLength,
            FileName = message.FileName ?? Path.GetFileName(blobName),
            FileType = fileType,
            Content = content,
        };
    }

    private string InferFileType(string container)
    {
        if (string.Equals(container, _options.ConsultationDocumentsContainer, StringComparison.OrdinalIgnoreCase))
            return ConsultationFileTypes.Document;
        if (string.Equals(container, _options.ConsultationAudioContainer, StringComparison.OrdinalIgnoreCase))
            return ConsultationFileTypes.Audio;
        return "Unknown";
    }
}
