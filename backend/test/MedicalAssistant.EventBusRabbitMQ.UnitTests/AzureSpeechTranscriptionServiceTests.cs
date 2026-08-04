using System.Net;
using System.Text;
using MedicalAssistant.Transcription.Worker.Options;
using MedicalAssistant.Transcription.Worker.Speech;
using MedicalAssistant.Transcription.Worker.Storage;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class AzureSpeechTranscriptionServiceTests
{
    [Fact]
    public async Task TranscribeAsync_posts_fast_transcription_request_without_sensitive_filename()
    {
        using var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"combinedPhrases":[{"text":"hello patient"}]}""",
                Encoding.UTF8,
                "application/json")
        });
        var service = CreateService(handler);
        await using var audio = new MemoryStream([1, 2, 3]);

        var result = await service.TranscribeAsync(
            new ConsultationAudioBlob(audio, "audio/wav", 3),
            CancellationToken.None);

        Assert.Equal("hello patient", result.TranscriptText);
        Assert.Equal("en-US", result.LanguageCode);
        Assert.Equal("https://westeurope.api.cognitive.microsoft.com/speechtotext/transcriptions:transcribe?api-version=2025-10-15", handler.Request!.RequestUri!.ToString());
        var body = handler.RequestBody!;
        Assert.Contains("safe-audio", body);
        Assert.DoesNotContain("patient", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private://", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, SpeechTranscriptionFailureCategory.Transient, "speech-throttled")]
    [InlineData(HttpStatusCode.InternalServerError, SpeechTranscriptionFailureCategory.Transient, "speech-service-unavailable")]
    [InlineData(HttpStatusCode.UnsupportedMediaType, SpeechTranscriptionFailureCategory.Permanent, "speech-unsupported-audio")]
    [InlineData(HttpStatusCode.BadRequest, SpeechTranscriptionFailureCategory.Permanent, "speech-invalid-request")]
    public async Task TranscribeAsync_classifies_provider_failures_without_leaking_response_body(
        HttpStatusCode statusCode,
        SpeechTranscriptionFailureCategory expectedCategory,
        string expectedCode)
    {
        using var handler = new RecordingHandler(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("sensitive provider body with transcript preview")
        });
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<SpeechTranscriptionFailureException>(() =>
            service.TranscribeAsync(
                new ConsultationAudioBlob(new MemoryStream([1]), "audio/wav", 1),
                CancellationToken.None));

        Assert.Equal(expectedCategory, exception.Category);
        Assert.Equal(expectedCode, exception.Code);
        Assert.DoesNotContain("sensitive provider body", exception.Message);
        Assert.DoesNotContain("transcript preview", exception.Message);
    }

    [Fact]
    public async Task TranscribeAsync_classifies_empty_transcript_as_permanent_failure()
    {
        using var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"combinedPhrases":[{"text":"   "}]}""")
        });
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<SpeechTranscriptionFailureException>(() =>
            service.TranscribeAsync(
                new ConsultationAudioBlob(new MemoryStream([1]), "audio/wav", 1),
                CancellationToken.None));

        Assert.Equal(SpeechTranscriptionFailureCategory.Permanent, exception.Category);
        Assert.Equal("speech-empty-transcript", exception.Code);
    }

    private static AzureSpeechTranscriptionService CreateService(HttpMessageHandler handler)
    {
        return new AzureSpeechTranscriptionService(
            new HttpClient(handler),
            Options.Create(new AzureSpeechTranscriptionOptions
            {
                Key = "test-key",
                Region = "westeurope",
                LanguageCode = "en-US",
                ApiVersion = "2025-10-15",
                Timeout = TimeSpan.FromSeconds(5),
                Phrases = ["aspirin", "hypertension"]
            }));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public RecordingHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return _response;
        }
    }
}
