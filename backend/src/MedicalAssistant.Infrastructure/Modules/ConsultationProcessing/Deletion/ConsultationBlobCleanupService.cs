using MedicalAssistant.Application.Contracts.Storage;
using MedicalAssistant.Application.Helpers;
using MedicalAssistant.Application.Models;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Infrastructure.BlobStorage;

public sealed class ConsultationBlobCleanupService : IConsultationBlobCleanupService
{
    private readonly IBlobStorageService _blobStorageService;
    private readonly BlobStorageSettings _settings;

    public ConsultationBlobCleanupService(
        IBlobStorageService blobStorageService,
        IOptions<BlobStorageSettings> settings)
    {
        _blobStorageService = blobStorageService;
        _settings = settings.Value;
    }

    public async Task DeleteIfExistsAsync(
        IReadOnlyCollection<string> objectReferences,
        CancellationToken cancellationToken = default)
    {
        foreach (var objectReference in objectReferences.Where(reference => !string.IsNullOrWhiteSpace(reference)))
        {
            var deleted = await TryDeleteFromConfiguredContainerAsync(
                objectReference,
                _settings.ConsultationAudioContainer,
                cancellationToken);
            deleted |= await TryDeleteFromConfiguredContainerAsync(
                objectReference,
                _settings.ConsultationDocumentsContainer,
                cancellationToken);

            if (!deleted && TryGetPrivateObjectName(objectReference, out var privateObjectName))
            {
                await _blobStorageService.DeleteAsync(
                    _settings.ConsultationAudioContainer,
                    privateObjectName,
                    cancellationToken);
                await _blobStorageService.DeleteAsync(
                    _settings.ConsultationDocumentsContainer,
                    privateObjectName,
                    cancellationToken);
            }
        }
    }

    private async Task<bool> TryDeleteFromConfiguredContainerAsync(
        string objectReference,
        string container,
        CancellationToken cancellationToken)
    {
        var blobName = BlobUriHelper.TryExtractBlobName(objectReference, container);
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return false;
        }

        await _blobStorageService.DeleteAsync(container, blobName, cancellationToken);
        return true;
    }

    private static bool TryGetPrivateObjectName(string objectReference, out string objectName)
    {
        objectName = string.Empty;
        if (!Uri.TryCreate(objectReference, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, "private", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var host = Uri.UnescapeDataString(uri.Host).Trim('/');
        var path = Uri.UnescapeDataString(uri.AbsolutePath).Trim('/');
        objectName = string.Join(
            '/',
            new[] { host, path }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return !string.IsNullOrWhiteSpace(objectName);
    }
}
