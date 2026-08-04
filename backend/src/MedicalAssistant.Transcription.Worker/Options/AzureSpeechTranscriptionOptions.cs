namespace MedicalAssistant.Transcription.Worker.Options;

public sealed class AzureSpeechTranscriptionOptions
{
    public const string SectionName = "TranscriptionWorker:AzureSpeech";

    public string Key { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "en-US";
    public string ApiVersion { get; set; } = "2025-10-15";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    public string[] Phrases { get; set; } = [];
}
