namespace MedicalAssistant.Transcriber.Options;

public sealed class AzureSpeechOptions
{
    public const string SectionName = "AzureSpeech";

    /// <summary>Azure Speech resource subscription key.</summary>
    public string? Key { get; set; }

    /// <summary>Azure Speech region (e.g. westeurope).</summary>
    public string Region { get; set; } = "westeurope";

    /// <summary>
    /// Speech recognition locale for transcription (speech-to-text), e.g. el-GR.
    /// This is not a translation target language.
    /// </summary>
    public string Locale { get; set; } = "el-GR";

    /// <summary>Speech to text REST API version for fast transcription.</summary>
    public string ApiVersion { get; set; } = "2025-10-15";

    /// <summary>
    /// Comma-separated words/phrases biased for recognition (fast transcription phrase list).
    /// Example: "Contoso,aspirin,hypertension"
    /// </summary>
    public string? Phrases { get; set; }
}
