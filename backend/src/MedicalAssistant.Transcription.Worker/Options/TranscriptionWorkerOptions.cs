namespace MedicalAssistant.Transcription.Worker.Options;

public sealed class TranscriptionWorkerOptions
{
    public const string SectionName = "TranscriptionWorker";

    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxConcurrentTranscriptions { get; set; } = 1;
}
