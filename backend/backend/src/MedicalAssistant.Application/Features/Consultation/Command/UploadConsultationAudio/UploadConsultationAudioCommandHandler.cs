using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Contracts.Storage;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Notifications;
using MedicalAssistant.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Features.Consultation.Command.UploadConsultationAudio;

public class UploadConsultationAudioCommandHandler : IRequestHandler<UploadConsultationAudioCommand, ConsultationDto>
{
    private readonly IConsultationRepository _consultationRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;
    private readonly IMediator _mediator;
    private readonly BlobStorageSettings _blobSettings;

    public UploadConsultationAudioCommandHandler(
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

    public async Task<ConsultationDto> Handle(UploadConsultationAudioCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var consultation = await _consultationRepository.GetConsultationForDoctorAsync(request.ConsultationId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Consultation), request.ConsultationId);

        if (consultation.Status is ConsultationStatus.Transcribing or ConsultationStatus.Completed)
            throw new BadRequestException($"Cannot upload audio when consultation status is {consultation.Status}.");

        var extension = Path.GetExtension(request.AudioFile.FileName);
        if (string.IsNullOrEmpty(extension))
            extension = ".webm";

        var blobName = $"consultations/{consultation.Id}/audio/{Guid.NewGuid():N}{extension}";

        await using var stream = request.AudioFile.OpenReadStream();
        var blobUri = await _blobStorageService.UploadAsync(
            _blobSettings.ConsultationAudioContainer,
            blobName,
            stream,
            request.AudioFile.ContentType,
            cancellationToken);

        consultation.MarkAudioUploaded(blobUri, request.AudioFile.ContentType, request.DurationSeconds);
        await _consultationRepository.UpdateAsync(consultation);

        await _mediator.Publish(new ConsultationAudioUploadedNotification
        {
            ConsultationId = consultation.Id,
            DoctorId = doctorId,
            AudioBlobUri = blobUri,
            CorrelationId = Guid.NewGuid().ToString("N")
        }, cancellationToken);

        return _mapper.Map<ConsultationDto>(consultation);
    }
}
