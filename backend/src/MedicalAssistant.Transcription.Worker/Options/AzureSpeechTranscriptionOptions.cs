namespace MedicalAssistant.Transcription.Worker.Options;

public sealed class AzureSpeechTranscriptionOptions
{
    public const string SectionName = "TranscriptionWorker:AzureSpeech";

    public string Region { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "en-US";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}
