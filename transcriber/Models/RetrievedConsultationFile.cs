namespace MedicalAssistant.Transcriber.Models;

public sealed class RetrievedConsultationFile
{
    public required string BlobUri { get; init; }
    public required string Container { get; init; }
    public required string BlobName { get; init; }
    public required string ContentType { get; init; }
    public required long ByteLength { get; init; }
    public string? FileName { get; init; }
    public required string FileType { get; init; }
    public required byte[] Content { get; init; }
}
