using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Contracts.Storage;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Models;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationAudio;

public class GetConsultationAudioQueryHandler
    : IRequestHandler<GetConsultationAudioQuery, ConsultationAudioResult?>
{
    private readonly IConsultationAccess _consultationRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IUserService _userService;
    private readonly BlobStorageSettings _blobSettings;

    public GetConsultationAudioQueryHandler(
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

    public async Task<ConsultationAudioResult?> Handle(
        GetConsultationAudioQuery request,
        CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var consultation = await _consultationRepository.GetConsultationForDoctorAsync(
                request.ConsultationId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Consultation), request.ConsultationId);

        if (string.IsNullOrWhiteSpace(consultation.AudioBlobUri))
            return null;

        var blobName = ExtractBlobName(consultation.AudioBlobUri, _blobSettings.ConsultationAudioContainer);
        var stream = await _blobStorageService.DownloadAsync(
            _blobSettings.ConsultationAudioContainer,
            blobName,
            cancellationToken);

        return new ConsultationAudioResult
        {
            Content = stream,
            ContentType = string.IsNullOrWhiteSpace(consultation.AudioContentType)
                ? "application/octet-stream"
                : consultation.AudioContentType,
        };
    }

    internal static string ExtractBlobName(string blobUri, string container)
    {
        var uri = new Uri(blobUri);
        var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
        var marker = container.Trim('/') + "/";
        var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            throw new BadRequestException("Stored audio URI is invalid.");

        return path[(index + marker.Length)..];
    }
}
