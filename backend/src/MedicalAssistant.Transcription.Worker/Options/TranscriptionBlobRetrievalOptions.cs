namespace MedicalAssistant.Transcription.Worker.Options;

public sealed class TranscriptionBlobRetrievalOptions
{
    public const string SectionName = "TranscriptionWorker:BlobRetrieval";

    public string ConsultationAudioContainer { get; set; } = "consultation-audio";
    public long MaxBytes { get; set; } = 100 * 1024 * 1024;
}
