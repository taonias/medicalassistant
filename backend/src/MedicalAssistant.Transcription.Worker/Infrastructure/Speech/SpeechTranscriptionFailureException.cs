namespace MedicalAssistant.Transcription.Worker.Speech;

public sealed class SpeechTranscriptionFailureException : Exception
{
    public SpeechTranscriptionFailureException(
        SpeechTranscriptionFailureCategory category,
        string code,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Category = category;
        Code = code;
    }

    public SpeechTranscriptionFailureCategory Category { get; }
    public string Code { get; }
}

public enum SpeechTranscriptionFailureCategory
{
    Transient = 0,
    Permanent = 1
}
