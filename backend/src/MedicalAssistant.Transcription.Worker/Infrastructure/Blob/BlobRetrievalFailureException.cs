namespace MedicalAssistant.Transcription.Worker.Storage;

public sealed class BlobRetrievalFailureException : Exception
{
    public BlobRetrievalFailureException(
        BlobRetrievalFailureCategory category,
        string code,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Category = category;
        Code = code;
    }

    public BlobRetrievalFailureCategory Category { get; }
    public string Code { get; }
}

public enum BlobRetrievalFailureCategory
{
    NotFound = 0,
    PolicyViolation = 1,
    Transient = 2
}
