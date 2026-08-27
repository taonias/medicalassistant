using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Contracts.Storage;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Models;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetConsultationDocument;

public record GetConsultationDocumentQuery(int ConsultationId) : IRequest<ConsultationDocumentResult?>;

public class ConsultationDocumentResult
{
    public required Stream Content { get; set; }
    public required string ContentType { get; set; }
    public string? FileName { get; set; }
}

public class GetConsultationDocumentQueryHandler
    : IRequestHandler<GetConsultationDocumentQuery, ConsultationDocumentResult?>
{
    private readonly IConsultationAccess _consultationRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IUserService _userService;
    private readonly BlobStorageSettings _blobSettings;

    public GetConsultationDocumentQueryHandler(
        IConsultationAccess consultationRepository,
        IBlobStorageService blobStorageService,
        IUserService userService,
        IOptions<BlobStorageSettings> blobSettings)
    {
        _consultationRepository = consultationRepository;
        _blobStorageService = blobStorageService;
        _userService = userService;
        _blobSettings = blobSettings.Value;
    }

    public async Task<ConsultationDocumentResult?> Handle(
        GetConsultationDocumentQuery request,
        CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var consultation = await _consultationRepository.GetConsultationForDoctorAsync(
                request.ConsultationId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Consultation), request.ConsultationId);

        if (string.IsNullOrWhiteSpace(consultation.DocumentBlobUri))
            return null;

        var blobName = ExtractBlobName(
            consultation.DocumentBlobUri,
            _blobSettings.ConsultationDocumentsContainer);
        var stream = await _blobStorageService.DownloadAsync(
            _blobSettings.ConsultationDocumentsContainer,
            blobName,
            cancellationToken);

        return new ConsultationDocumentResult
        {
            Content = stream,
            ContentType = string.IsNullOrWhiteSpace(consultation.DocumentContentType)
                ? "application/pdf"
                : consultation.DocumentContentType,
            FileName = consultation.DocumentFileName,
        };
    }

    internal static string ExtractBlobName(string blobUri, string container)
    {
        var uri = new Uri(blobUri);
        var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
        var marker = container.Trim('/') + "/";
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            throw new BadRequestException("Stored document URI is invalid.");

        return path[(index + marker.Length)..];
    }
}
