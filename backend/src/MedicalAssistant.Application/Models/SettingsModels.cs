namespace MedicalAssistant.Application.Models;

public class JwtSettings
{
    public required string Key { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public int DurationInMinutes { get; set; } = 60;
}

public class BlobStorageSettings
{
    public string? ConnectionString { get; set; }
    public string ConsultationAudioContainer { get; set; } = "consultation-audio";
    public string ConsultationDocumentsContainer { get; set; } = "consultation-documents";
    public string TranscriptsContainer { get; set; } = "transcripts";
}

public class AiModuleSettings
{
    public required string BaseUrl { get; set; }
    public required string ApiKey { get; set; }
    public string ApiBaseUrl { get; set; } = "https://localhost:7001";
    public int ChatTimeoutSeconds { get; set; } = 60;
    public int TranscriptionSubmitTimeoutSeconds { get; set; } = 10;
}

public class AiCallbackSettings
{
    public required string ApiKey { get; set; }
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
