namespace MedicalAssistant.Application.Models;

public class JwtSettings
{
    public required string Key { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public int DurationInMinutes { get; set; } = 60;
}

public class AdminSeedSettings
{
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
}

public class BlobStorageSettings
{
    public string? ConnectionString { get; set; }
    public string ConsultationAudioContainer { get; set; } = "consultation-audio";
    public string ConsultationDocumentsContainer { get; set; } = "consultation-documents";
    public string TranscriptsContainer { get; set; } = "transcripts";
}

public class AppSettings
{
    public string[] AllowedAudioContentTypes { get; set; } =
        ["audio/wav", "audio/mpeg", "audio/mp3", "audio/webm", "audio/ogg"];
    public long MaxAudioFileSizeBytes { get; set; } = 104_857_600;

    public string[] AllowedDocumentContentTypes { get; set; } =
        ["application/pdf"];
    public long MaxDocumentFileSizeBytes { get; set; } = 104_857_600;
}
