namespace MedicalAssistant.Transcriber.Options;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string? ConnectionString { get; set; }
    public string ConsultationAudioContainer { get; set; } = "audio";
    public string ConsultationDocumentsContainer { get; set; } = "pdf";
}
