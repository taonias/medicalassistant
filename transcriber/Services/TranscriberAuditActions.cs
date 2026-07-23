namespace MedicalAssistant.Transcriber.Services;

public static class TranscriberAuditActions
{
    public const string ProcessStarted = "Transcriber.ProcessStarted";
    public const string MessageParsed = "Transcriber.MessageParsed";
    public const string BlobRetrieveStarted = "Transcriber.BlobRetrieveStarted";
    public const string BlobRetrieved = "Transcriber.BlobRetrieved";
    public const string TranscriptStarted = "Transcriber.TranscriptStarted";
    public const string DuplicateSkipped = "Transcriber.DuplicateSkipped";
    public const string ConsultationMarkedTranscribing = "Transcriber.ConsultationMarkedTranscribing";
    public const string SpeechStarted = "Transcriber.SpeechStarted";
    public const string SpeechCompleted = "Transcriber.SpeechCompleted";
    public const string DocumentPlaceholderUsed = "Transcriber.DocumentPlaceholderUsed";
    public const string TranscriptSaved = "Transcriber.TranscriptSaved";
    public const string TranscriptReadyPublished = "Transcriber.TranscriptReadyPublished";
    public const string ConsultationMarkedTranscribed = "Transcriber.ConsultationMarkedTranscribed";
    public const string ProcessCompleted = "Transcriber.ProcessCompleted";
    public const string ProcessFailed = "Transcriber.ProcessFailed";
}
