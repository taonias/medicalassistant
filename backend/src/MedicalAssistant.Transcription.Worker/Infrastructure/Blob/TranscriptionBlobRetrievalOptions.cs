namespace MedicalAssistant.Transcription.Worker.Options;

public sealed class TranscriptionBlobRetrievalOptions
{
    public const string SectionName = "TranscriptionWorker:BlobRetrieval";

    public string ConnectionString { get; set; } = string.Empty;
    public string ConsultationAudioContainer { get; set; } = "consultation-audio";
    public long MaxBytes { get; set; } = 100 * 1024 * 1024;
    public string[] AllowedContentTypes { get; set; } =
    [
        "audio/wav",
        "audio/mpeg",
        "audio/mp3",
        "audio/webm",
        "audio/ogg"
    ];
}
