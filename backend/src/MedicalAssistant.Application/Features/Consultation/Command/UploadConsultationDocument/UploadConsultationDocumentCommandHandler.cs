using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Contracts.Storage;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Models.Messaging;
using MedicalAssistant.Application.Notifications;
using MedicalAssistant.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Features.Consultation.Command.UploadConsultationDocument;

public class UploadConsultationDocumentCommandHandler
    : IRequestHandler<UploadConsultationDocumentCommand, ConsultationDto>
{
    private readonly IConsultationRepository _consultationRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;
    private readonly IMediator _mediator;
    private readonly BlobStorageSettings _blobSettings;

    public UploadConsultationDocumentCommandHandler(
        IConsultationRepository consultationRepository,
        IBlobStorageService blobStorageService,
        IUserService userService,
        IMapper mapper,
        IMediator mediator,
        IOptions<BlobStorageSettings> blobSettings)
    {
        _consultationRepository = consultationRepository;
        _blobStorageService = blobStorageService;
        _userService = userService;
        _mapper = mapper;
        _mediator = mediator;
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
        await _consultationRepository.UpdateAsync(consultation);

        await _mediator.Publish(
            new ConsultationReadyForProcessingNotification
            {
                EventType = ConsultationProcessingEventTypes.FileUploaded,
                ConsultationId = consultation.Id,
                PatientId = consultation.PatientId,
                DoctorId = consultation.DoctorId,
                Status = consultation.Status.ToString(),
                FileType = ConsultationFileTypes.Document,
                BlobUri = consultation.DocumentBlobUri,
                ContentType = consultation.DocumentContentType,
                FileName = consultation.DocumentFileName,
                CorrelationId = Guid.NewGuid().ToString("N"),
            },
            cancellationToken);

        return _mapper.Map<ConsultationDto>(consultation);
    }
}
