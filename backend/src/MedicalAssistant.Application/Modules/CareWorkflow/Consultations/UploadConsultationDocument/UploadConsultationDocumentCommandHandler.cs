using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Contracts.Storage;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Services;
using MedicalAssistant.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Features.Consultation.Command.UploadConsultationDocument;

public class UploadConsultationDocumentCommandHandler
    : IRequestHandler<UploadConsultationDocumentCommand, ConsultationDto>
{
    private readonly IConsultationFileRegistration _consultationRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;
    private readonly BlobStorageSettings _blobSettings;

    public UploadConsultationDocumentCommandHandler(
        IConsultationFileRegistration consultationRepository,
        IBlobStorageService blobStorageService,
        IUserService userService,
        IMapper mapper,
        IOptions<BlobStorageSettings> blobSettings)
    {
        _consultationRepository = consultationRepository;
        _blobStorageService = blobStorageService;
        _userService = userService;
        _mapper = mapper;
        _blobSettings = blobSettings.Value;
    }

    public async Task<ConsultationDto> Handle(
        UploadConsultationDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var consultation = await _consultationRepository.GetConsultationForDoctorAsync(request.ConsultationId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Consultation), request.ConsultationId);

        if (consultation.Status is ConsultationStatus.Transcribing or ConsultationStatus.Completed)
            throw new BadRequestException($"Cannot upload document when consultation status is {consultation.Status}.");

        await using var uploadStream = request.DocumentFile.OpenReadStream();
        var extension = Path.GetExtension(request.DocumentFile.FileName);
        if (string.IsNullOrEmpty(extension))
            extension = ".pdf";

        var blobName = $"consultations/{consultation.Id}/documents/{Guid.NewGuid():N}{extension}";
        var contentType = string.IsNullOrWhiteSpace(request.DocumentFile.ContentType)
            ? "application/pdf"
            : request.DocumentFile.ContentType;

        var blobUri = await _blobStorageService.UploadAsync(
            _blobSettings.ConsultationDocumentsContainer,
            blobName,
            uploadStream,
            contentType,
            cancellationToken);

        consultation.MarkDocumentUploaded(
            blobUri,
            contentType,
            Path.GetFileName(request.DocumentFile.FileName));
        var outboxMessage = ConsultationOutboxFactory.DocumentUploaded(
            consultation,
            blobName,
            Guid.NewGuid().ToString("N"));
        await _consultationRepository.UpdateWithOutboxAsync(
            consultation,
            outboxMessage,
            cancellationToken);

        return _mapper.Map<ConsultationDto>(consultation);
    }
}
