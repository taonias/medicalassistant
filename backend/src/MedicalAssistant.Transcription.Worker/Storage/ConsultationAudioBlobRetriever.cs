using MedicalAssistant.Transcription.Worker.Options;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcription.Worker.Storage;

public sealed class ConsultationAudioBlobRetriever : IConsultationAudioBlobRetriever
{
    private readonly IPrivateBlobObjectClient _objectClient;
    private readonly TranscriptionBlobRetrievalOptions _options;

    public ConsultationAudioBlobRetriever(
        IPrivateBlobObjectClient objectClient,
        IOptions<TranscriptionBlobRetrievalOptions> options)
    {
        _objectClient = objectClient;
        _options = options.Value;
    }

    public async Task<ConsultationAudioBlob> OpenReadAsync(
        string storageObjectReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageObjectReference))
        {
            throw new BlobRetrievalFailureException(
                BlobRetrievalFailureCategory.PolicyViolation,
                "missing-object-reference",
                "Audio object reference is required.");
        }

        var blob = await _objectClient.OpenReadAsync(storageObjectReference, cancellationToken);

        if (!_options.AllowedContentTypes.Contains(blob.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new BlobRetrievalFailureException(
                BlobRetrievalFailureCategory.PolicyViolation,
                "unsupported-content-type",
                "Audio object content type is not supported for transcription.");
        }

        if (blob.ContentLength > _options.MaxBytes)
        {
            throw new BlobRetrievalFailureException(
                BlobRetrievalFailureCategory.PolicyViolation,
                "audio-too-large",
                "Audio object exceeds the configured transcription size limit.");
        }

        return new ConsultationAudioBlob(
            blob.Content,
            blob.ContentType,
            blob.ContentLength);
    }
}
