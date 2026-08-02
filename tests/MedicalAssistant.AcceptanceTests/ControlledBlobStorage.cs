using System.Collections.Concurrent;
using MedicalAssistant.Application.Contracts.Storage;

namespace MedicalAssistant.AcceptanceTests;

/// <summary>Private, in-memory Blob adapter for synthetic acceptance fixtures.</summary>
public sealed class ControlledBlobStorage : IBlobStorageService
{
    private readonly ConcurrentDictionary<string, StoredBlob> _blobs = new(StringComparer.Ordinal);

    public async Task<string> UploadAsync(
        string container,
        string blobName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        var reference = BuildReference(container, blobName);
        _blobs[reference] = new StoredBlob(copy.ToArray(), contentType);
        return reference;
    }

    public Task<Stream> DownloadAsync(
        string container,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var reference = BuildReference(container, blobName);
        if (!_blobs.TryGetValue(reference, out var blob))
            throw new FileNotFoundException("Synthetic Blob object was not found.");
        return Task.FromResult<Stream>(new MemoryStream(blob.Content, writable: false));
    }

    public Task DeleteAsync(string container, string blobName, CancellationToken cancellationToken = default)
    {
        _blobs.TryRemove(BuildReference(container, blobName), out _);
        return Task.CompletedTask;
    }

    public Task<string> GenerateSasUriAsync(
        string container,
        string blobName,
        TimeSpan expiry,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(BuildReference(container, blobName));

    public bool Contains(string? opaqueReference) =>
        opaqueReference is not null && _blobs.ContainsKey(opaqueReference);

    private static string BuildReference(string container, string blobName) =>
        $"controlled-blob://{Uri.EscapeDataString(container)}/{Uri.EscapeDataString(blobName)}";

    private sealed record StoredBlob(byte[] Content, string ContentType);
}
