using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MedicalAssistant.Transcriber.Models;
using MedicalAssistant.Transcriber.Options;
using MedicalAssistant.Transcriber.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Transcriber.Services;

/// <summary>
/// Transcribes consultation audio via Azure Speech fast transcription REST API.
/// Used instead of SpeechRecognizer + compressed PushStream because WebM input
/// requires GStreamer and the SDK constructor can hang indefinitely without it,
/// which also causes RabbitMQ to redeliver the same message.
/// </summary>
public sealed class AzureSpeechTranscriptionService : ISpeechTranscriptionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly AzureSpeechOptions _options;
    private readonly ILogger<AzureSpeechTranscriptionService> _logger;

    public AzureSpeechTranscriptionService(
        HttpClient httpClient,
        IOptions<AzureSpeechOptions> options,
        ILogger<AzureSpeechTranscriptionService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> TranscribeAsync(
        RetrievedConsultationFile file,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Key))
            throw new InvalidOperationException("AzureSpeech:Key is not configured.");
        if (string.IsNullOrWhiteSpace(_options.Region))
            throw new InvalidOperationException("AzureSpeech:Region is not configured.");

        if (file.Content is null || file.Content.Length == 0)
            throw new InvalidOperationException("Retrieved file has no audio content to transcribe.");

        if (string.IsNullOrWhiteSpace(_options.Locale))
            throw new InvalidOperationException("AzureSpeech:Locale is not configured.");

        // Fast transcription = speech-to-text (not translation). Locale sets the recognition language.
        // Phrase list requires API version 2025-10-15 or later.
        var apiVersion = string.IsNullOrWhiteSpace(_options.ApiVersion)
            ? "2025-10-15"
            : _options.ApiVersion;

        var url =
            $"https://{_options.Region}.api.cognitive.microsoft.com/speechtotext/transcriptions:transcribe?api-version={Uri.EscapeDataString(apiVersion)}";

        using var form = new MultipartFormDataContent();

        var audioContent = new ByteArrayContent(file.Content);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType.Split(';', 2)[0].Trim());

        var fileName = string.IsNullOrWhiteSpace(file.FileName)
            ? Path.GetFileName(file.BlobName)
            : file.FileName;
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "audio.webm";

        form.Add(audioContent, "audio", fileName);

        var phrases = ParsePhrases(_options.Phrases);
        object definition = phrases.Count > 0
            ? new
            {
                locales = new[] { _options.Locale },
                phraseList = new { phrases },
            }
            : new
            {
                locales = new[] { _options.Locale },
            };

        var definitionJson = JsonSerializer.Serialize(definition);
        form.Add(new StringContent(definitionJson, Encoding.UTF8, "application/json"), "definition");

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        request.Headers.Add("Ocp-Apim-Subscription-Key", _options.Key);

        _logger.LogInformation(
            "Calling Azure Speech fast transcription (speech-to-text, locale={Locale}, phraseCount={PhraseCount}) for {FileName} ({ByteLength} bytes, {ContentType}).",
            _options.Locale,
            phrases.Count,
            fileName,
            file.ByteLength,
            file.ContentType);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Azure Speech transcription failed with {StatusCode}: {Body}",
                (int)response.StatusCode,
                Truncate(responseBody, 500));
            throw new InvalidOperationException(
                $"Azure Speech transcription failed with status {(int)response.StatusCode}.");
        }

        var result = JsonSerializer.Deserialize<FastTranscriptionResponse>(responseBody, JsonOptions)
            ?? throw new InvalidOperationException("Azure Speech returned an empty transcription response.");

        var text = result.CombinedPhrases is { Count: > 0 }
            ? string.Join(" ", result.CombinedPhrases
                .Select(p => p.Text?.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t)))
            : string.Join(" ", (result.Phrases ?? [])
                .Select(p => p.Text?.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t)));

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Azure Speech returned no transcribed text.");

        return text.Trim();
    }

    private static List<string> ParsePhrases(string? phrasesCsv)
    {
        if (string.IsNullOrWhiteSpace(phrasesCsv))
            return [];

        return phrasesCsv
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

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
