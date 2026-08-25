using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MedicalAssistant.Transcription.Worker.Options;
using MedicalAssistant.Transcription.Worker.Storage;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcription.Worker.Speech;

public sealed class AzureSpeechTranscriptionService : ISpeechTranscriptionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly AzureSpeechTranscriptionOptions _options;

    public AzureSpeechTranscriptionService(
        HttpClient httpClient,
        IOptions<AzureSpeechTranscriptionOptions> options)
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
                "speech-timeout",
                "Azure Speech transcription timed out.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Transient,
                "speech-network-error",
                "Azure Speech transcription request failed.",
                ex);
        }
        catch (JsonException ex)
        {
            throw new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Permanent,
                "speech-invalid-response",
                "Azure Speech returned an invalid transcription response.",
                ex);
        }
    }

    private HttpRequestMessage BuildRequest(ConsultationAudioBlob audio)
    {
        var url =
            $"https://{_options.Region}.api.cognitive.microsoft.com/speechtotext/transcriptions:transcribe?api-version={Uri.EscapeDataString(_options.ApiVersion)}";

        var form = new MultipartFormDataContent();
        var audioContent = new StreamContent(audio.Content);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(audio.ContentType)
                ? "application/octet-stream"
                : audio.ContentType.Split(';', 2)[0].Trim());
        form.Add(audioContent, "audio", "safe-audio");

        var phrases = _options.Phrases
            .Where(phrase => !string.IsNullOrWhiteSpace(phrase))
            .Select(phrase => phrase.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        object definition = phrases.Length > 0
            ? new
            {
                locales = new[] { _options.LanguageCode },
                phraseList = new { phrases }
            }
            : new
            {
                locales = new[] { _options.LanguageCode }
            };

        form.Add(
            new StringContent(JsonSerializer.Serialize(definition), Encoding.UTF8, "application/json"),
            "definition");

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = form
        };
        request.Headers.Add("Ocp-Apim-Subscription-Key", _options.Key);
        return request;
    }

    private static SpeechTranscriptionFailureException FailureForStatusCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.TooManyRequests => new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Transient,
                "speech-throttled",
                "Azure Speech throttled the transcription request."),
            HttpStatusCode.RequestTimeout or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout or
            HttpStatusCode.InternalServerError => new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Transient,
                "speech-service-unavailable",
                "Azure Speech is temporarily unavailable."),
            HttpStatusCode.UnsupportedMediaType => new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Permanent,
                "speech-unsupported-audio",
                "Azure Speech rejected the audio format."),
            HttpStatusCode.BadRequest => new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Permanent,
                "speech-invalid-request",
                "Azure Speech rejected the transcription request."),
            _ => new SpeechTranscriptionFailureException(
                (int)statusCode >= 500
                    ? SpeechTranscriptionFailureCategory.Transient
                    : SpeechTranscriptionFailureCategory.Permanent,
                (int)statusCode >= 500 ? "speech-service-unavailable" : "speech-provider-rejected",
                "Azure Speech did not accept the transcription request.")
        };
    }

    private static string ExtractTranscriptText(string responseBody)
    {
        var result = JsonSerializer.Deserialize<FastTranscriptionResponse>(responseBody, JsonOptions)
            ?? throw new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Permanent,
                "speech-invalid-response",
                "Azure Speech returned an invalid transcription response.");

        var text = result.CombinedPhrases is { Count: > 0 }
            ? string.Join(
                " ",
                result.CombinedPhrases
                    .Select(phrase => phrase.Text?.Trim())
                    .Where(phrase => !string.IsNullOrWhiteSpace(phrase)))
            : string.Join(
                " ",
                (result.Phrases ?? [])
                    .Select(phrase => phrase.Text?.Trim())
                    .Where(phrase => !string.IsNullOrWhiteSpace(phrase)));

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new SpeechTranscriptionFailureException(
                SpeechTranscriptionFailureCategory.Permanent,
                "speech-empty-transcript",
                "Azure Speech returned no transcript text.");
        }

        return text.Trim();
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.Key))
            throw MissingConfiguration("speech-key-missing", "Azure Speech key is not configured.");
        if (string.IsNullOrWhiteSpace(_options.Region))
            throw MissingConfiguration("speech-region-missing", "Azure Speech region is not configured.");
        if (string.IsNullOrWhiteSpace(_options.LanguageCode))
            throw MissingConfiguration("speech-language-missing", "Azure Speech language is not configured.");
        if (string.IsNullOrWhiteSpace(_options.ApiVersion))
            throw MissingConfiguration("speech-api-version-missing", "Azure Speech API version is not configured.");
    }

    private static SpeechTranscriptionFailureException MissingConfiguration(string code, string message)
    {
        return new SpeechTranscriptionFailureException(
            SpeechTranscriptionFailureCategory.Permanent,
            code,
            message);
    }

    private sealed class FastTranscriptionResponse
    {
        [JsonPropertyName("combinedPhrases")]
        public List<CombinedPhrase>? CombinedPhrases { get; set; }

        [JsonPropertyName("phrases")]
        public List<Phrase>? Phrases { get; set; }
    }

    private sealed class CombinedPhrase
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class Phrase
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
