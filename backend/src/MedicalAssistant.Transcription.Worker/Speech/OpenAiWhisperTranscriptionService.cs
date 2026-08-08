using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using MedicalAssistant.Transcription.Worker.Options;
using MedicalAssistant.Transcription.Worker.Storage;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcription.Worker.Speech;

/// <summary>
/// Transcribes consultation audio with the OpenAI speech-to-text API
/// (POST {BaseUrl}/audio/transcriptions). Implements the same contract as
/// <see cref="AzureSpeechTranscriptionService"/> and is selected via
/// TranscriptionWorker:Provider = "OpenAiWhisper". Failures are classified as
/// Transient (retry via the worker's RabbitMQ retry queues) or Permanent, and
/// never carry provider response bodies into the exception message.
/// </summary>
public sealed class OpenAiWhisperTranscriptionService : ISpeechTranscriptionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiWhisperTranscriptionOptions _options;

    public OpenAiWhisperTranscriptionService(
        HttpClient httpClient,
        IOptions<OpenAiWhisperTranscriptionOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<SpeechTranscriptionResult> TranscribeAsync(
        ConsultationAudioBlob audio,
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.Timeout);

        try
        {
            using var request = BuildRequest(audio);
            using var response = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw FailureForStatusCode(response.StatusCode);

            var transcriptText = ExtractTranscriptText(responseBody);
            return new SpeechTranscriptionResult(transcriptText, _options.LanguageCode);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Transient,
                "whisper-timeout",
                "OpenAI Whisper transcription timed out.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Transient,
                "whisper-network-error",
                "OpenAI Whisper transcription request failed.",
                ex);
        }
        catch (JsonException ex)
        {
            throw new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Permanent,
                "whisper-invalid-response",
                "OpenAI Whisper returned an invalid transcription response.",
                ex);
        }
    }

    private HttpRequestMessage BuildRequest(ConsultationAudioBlob audio)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/audio/transcriptions";

        var contentType = string.IsNullOrWhiteSpace(audio.ContentType)
            ? "application/octet-stream"
            : audio.ContentType.Split(';', 2)[0].Trim();

        // OpenAI infers the audio format from the file name extension, so give the
        // part a real, supported extension derived from the stored content type.
        var fileName = $"consultation-audio{ExtensionForContentType(contentType)}";

        var form = new MultipartFormDataContent();
        var audioContent = new StreamContent(audio.Content);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(audioContent, "file", fileName);
        form.Add(new StringContent(_options.Model), "model");
        form.Add(new StringContent("json"), "response_format");

        var languageHint = IsoLanguage(_options.LanguageCode);
        if (!string.IsNullOrWhiteSpace(languageHint))
            form.Add(new StringContent(languageHint), "language");

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = form
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        return request;
    }

    private static SpeechTranscriptionFailureException FailureForStatusCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Permanent,
                "whisper-unauthorized",
                "OpenAI Whisper rejected the API key."),
            HttpStatusCode.TooManyRequests => new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Transient,
                "whisper-throttled",
                "OpenAI Whisper throttled the transcription request."),
            HttpStatusCode.RequestEntityTooLarge => new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Permanent,
                "whisper-audio-too-large",
                "OpenAI Whisper rejected the audio as too large."),
            HttpStatusCode.UnsupportedMediaType => new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Permanent,
                "whisper-unsupported-audio",
                "OpenAI Whisper rejected the audio format."),
            HttpStatusCode.BadRequest => new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Permanent,
                "whisper-invalid-request",
                "OpenAI Whisper rejected the transcription request."),
            HttpStatusCode.RequestTimeout or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout or
            HttpStatusCode.InternalServerError => new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Transient,
                "whisper-service-unavailable",
                "OpenAI Whisper is temporarily unavailable."),
            _ => new SpeechTranscriptionFailureException(
                (int)statusCode >= 500
                    ? SpeechTranscriptionFailureCategory.Transient
                    : SpeechTranscriptionFailureCategory.Permanent,
                (int)statusCode >= 500 ? "whisper-service-unavailable" : "whisper-provider-rejected",
                "OpenAI Whisper did not accept the transcription request.")
        };
    }

    private static string ExtractTranscriptText(string responseBody)
    {
        var result = JsonSerializer.Deserialize<WhisperTranscriptionResponse>(responseBody, JsonOptions)
            ?? throw new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Permanent,
                "whisper-invalid-response",
                "OpenAI Whisper returned an invalid transcription response.");

        if (string.IsNullOrWhiteSpace(result.Text))
        {
            throw new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Permanent,
                "whisper-empty-transcript",
                "OpenAI Whisper returned no transcript text.");
        }

        return result.Text.Trim();
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw MissingConfiguration("whisper-key-missing", "OpenAI Whisper API key is not configured.");
        if (string.IsNullOrWhiteSpace(_options.Model))
            throw MissingConfiguration("whisper-model-missing", "OpenAI Whisper model is not configured.");
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            throw MissingConfiguration("whisper-base-url-missing", "OpenAI Whisper base URL is not configured.");
    }

    private static SpeechTranscriptionFailureException MissingConfiguration(string code, string message)
    {
        return new SpeechTranscriptionFailureException(
            SpeechTranscriptionFailureCategory.Permanent,
            code,
            message);
    }

    private static string IsoLanguage(string languageCode) =>
        string.IsNullOrWhiteSpace(languageCode)
            ? string.Empty
            : languageCode.Split('-', 2)[0].Trim().ToLowerInvariant();

    private static string ExtensionForContentType(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "audio/wav" or "audio/x-wav" or "audio/wave" => ".wav",
            "audio/mpeg" or "audio/mp3" => ".mp3",
            "audio/mp4" or "audio/m4a" or "audio/x-m4a" => ".m4a",
            "audio/webm" => ".webm",
            "audio/ogg" or "audio/oga" => ".ogg",
            "audio/flac" or "audio/x-flac" => ".flac",
            _ => ".wav"
        };

    private sealed class WhisperTranscriptionResponse
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
