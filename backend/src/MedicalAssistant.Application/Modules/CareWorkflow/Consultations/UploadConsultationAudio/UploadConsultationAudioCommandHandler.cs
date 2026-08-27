using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Contracts.Storage;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetConsultationDetails;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Services;
using MedicalAssistant.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations.UploadConsultationAudio;

public class UploadConsultationAudioCommandHandler : IRequestHandler<UploadConsultationAudioCommand, ConsultationDto>
{
    private readonly IConsultationFileRegistration _consultationRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;
    private readonly BlobStorageSettings _blobSettings;

    public UploadConsultationAudioCommandHandler(
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
        var contentType = (request.AudioFile.ContentType ?? string.Empty).Split(';', 2)[0].Trim();
        if (string.IsNullOrEmpty(contentType))
            contentType = "audio/webm";

        await using var stream = request.AudioFile.OpenReadStream();
        var blobUri = await _blobStorageService.UploadAsync(
            _blobSettings.ConsultationAudioContainer,
            blobName,
            stream,
            contentType,
            cancellationToken);

        consultation.MarkAudioUploaded(blobUri, contentType, request.DurationSeconds);
        var correlationId = Guid.NewGuid().ToString("N");
        var outboxMessage = ConsultationOutboxFactory.AudioUploaded(
            consultation,
            blobName,
            correlationId);
        await _consultationRepository.UpdateWithOutboxAsync(
            consultation,
            outboxMessage,
            cancellationToken);

        return _mapper.Map<ConsultationDto>(consultation);
    }
}
