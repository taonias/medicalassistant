using MedicalAssistant.Application.Exceptions;

namespace MedicalAssistant.Application.Helpers;

public static class BlobUriHelper
{
    public static string? TryExtractBlobName(string? blobUri, string container)
    {
        if (string.IsNullOrWhiteSpace(blobUri) || string.IsNullOrWhiteSpace(container))
            return null;

        try
        {
            return ExtractBlobName(blobUri, container);
        }
        catch
        {
            return null;
        }
    }

    public static string ExtractBlobName(string blobUri, string container)
    {
        var uri = new Uri(blobUri);
        var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
        var marker = container.Trim('/') + "/";
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            throw new BadRequestException("Stored blob URI is invalid.");

        return path[(index + marker.Length)..];
    }
}
