namespace MedicalAssistant.Transcription.Worker.Options;

public sealed class OpenAiWhisperTranscriptionOptions
{
    public const string SectionName = "TranscriptionWorker:OpenAiWhisper";

    /// <summary>OpenAI API key (secret). When empty the service fails with a permanent, actionable "whisper-key-missing" outcome.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Speech-to-text model, e.g. whisper-1, gpt-4o-transcribe, gpt-4o-mini-transcribe.</summary>
    public string Model { get; set; } = "whisper-1";

    /// <summary>API base URL. Overridable for Azure OpenAI or a proxy. Must not end with a trailing slash.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>BCP-47 language returned on the transcript. The ISO-639-1 prefix (e.g. "en") is sent to Whisper as a hint.</summary>
    public string LanguageCode { get; set; } = "en-US";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}
