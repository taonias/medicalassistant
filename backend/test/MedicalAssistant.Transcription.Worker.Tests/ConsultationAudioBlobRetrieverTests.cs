using MedicalAssistant.Transcription.Worker.Options;
using MedicalAssistant.Transcription.Worker.Storage;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcription.Worker.Tests;

public class ConsultationAudioBlobRetrieverTests
{
    [Fact]
    public async Task OpenReadAsync_rejects_unsupported_content_type_without_leaking_object_reference()
    {
        var objectClient = new StubPrivateBlobObjectClient(
            new PrivateBlobObject(
                new MemoryStream([1, 2, 3]),
                "application/pdf",
                3));
        var retriever = CreateRetriever(objectClient);

        var exception = await Assert.ThrowsAsync<BlobRetrievalFailureException>(() =>
            retriever.OpenReadAsync(
                "private://consultations/very-sensitive-patient-name.wav",
                CancellationToken.None));

        Assert.Equal(BlobRetrievalFailureCategory.PolicyViolation, exception.Category);
        Assert.Equal("unsupported-content-type", exception.Code);
        Assert.DoesNotContain("very-sensitive-patient-name", exception.Message);
    }

    [Fact]
    public async Task OpenReadAsync_rejects_audio_larger_than_configured_limit()
    {
        var objectClient = new StubPrivateBlobObjectClient(
            new PrivateBlobObject(
                new MemoryStream([1, 2, 3]),
                "audio/wav",
                101));
        var retriever = CreateRetriever(objectClient, maxBytes: 100);

        var exception = await Assert.ThrowsAsync<BlobRetrievalFailureException>(() =>
            retriever.OpenReadAsync("private://consultations/10/audio.wav", CancellationToken.None));

        Assert.Equal(BlobRetrievalFailureCategory.PolicyViolation, exception.Category);
        Assert.Equal("audio-too-large", exception.Code);
    }

    [Fact]
    public async Task OpenReadAsync_returns_allowed_audio_stream()
    {
        await using var stream = new MemoryStream([1, 2, 3]);
        var objectClient = new StubPrivateBlobObjectClient(
            new PrivateBlobObject(stream, "audio/wav", 3));
        var retriever = CreateRetriever(objectClient);

        var result = await retriever.OpenReadAsync("private://consultations/10/audio.wav", CancellationToken.None);

        Assert.Equal("audio/wav", result.ContentType);
        Assert.Equal(3, result.ContentLength);
        Assert.Same(stream, result.Content);
    }

    private static ConsultationAudioBlobRetriever CreateRetriever(
        IPrivateBlobObjectClient objectClient,
        long maxBytes = 1024)
    {
        return new ConsultationAudioBlobRetriever(
            objectClient,
            Microsoft.Extensions.Options.Options.Create(new TranscriptionBlobRetrievalOptions
            {
                MaxBytes = maxBytes,
                AllowedContentTypes = ["audio/wav", "audio/mpeg"]
            }));
    }

    private sealed class StubPrivateBlobObjectClient : IPrivateBlobObjectClient
    {
        private readonly PrivateBlobObject _result;

        public StubPrivateBlobObjectClient(PrivateBlobObject result)
        {
            _result = result;
        }

        public Task<PrivateBlobObject> OpenReadAsync(
            string storageObjectReference,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }
    }
}
