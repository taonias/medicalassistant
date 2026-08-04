namespace MedicalAssistant.Transcription.Worker.Handlers;

public sealed class TranscriptionInboxClaimException : InvalidOperationException
{
    public TranscriptionInboxClaimException(string message) : base(message)
    {
    }
}
