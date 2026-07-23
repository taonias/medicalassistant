using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Features.Transcript.Queries.GetTranscript;
using MedicalAssistant.Application.Models.Messaging;
using MediatR;

namespace MedicalAssistant.Application.Features.Transcript.Command.UpdateTranscript;

public class UpdateTranscriptCommandHandler : IRequestHandler<UpdateTranscriptCommand, TranscriptDto>
{
    private readonly ITranscriptRepository _transcriptRepository;
    private readonly IConsultationRepository _consultationRepository;
    private readonly IUserService _userService;
    private readonly ITranscriptReadyPublisher _transcriptReadyPublisher;
    private readonly IAppLogger<UpdateTranscriptCommandHandler> _logger;
    private readonly IMapper _mapper;

    public UpdateTranscriptCommandHandler(
        ITranscriptRepository transcriptRepository,
        IConsultationRepository consultationRepository,
        IUserService userService,
        ITranscriptReadyPublisher transcriptReadyPublisher,
        IAppLogger<UpdateTranscriptCommandHandler> logger,
        IMapper mapper)
    {
        _transcriptRepository = transcriptRepository;
        _consultationRepository = consultationRepository;
        _userService = userService;
        _transcriptReadyPublisher = transcriptReadyPublisher;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task<TranscriptDto> Handle(UpdateTranscriptCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        _ = await _consultationRepository.GetConsultationForDoctorAsync(request.ConsultationId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Consultation), request.ConsultationId);

        var transcript = await _transcriptRepository.GetByConsultationIdAsync(request.ConsultationId)
            ?? throw new NotFoundException(nameof(Domain.Transcript), request.ConsultationId);

        if (string.IsNullOrWhiteSpace(transcript.TranscriptText))
            throw new BadRequestException("Transcript cannot be edited until it has been populated.");

        transcript.UpdateText(request.Transcript);
        await _transcriptRepository.UpdateAsync(transcript);

        var correlationId = Guid.NewGuid().ToString("N");
        try
        {
            await _transcriptReadyPublisher.PublishAsync(
                new TranscriptReadyMessage
                {
                    EventType = TranscriptReadyEventTypes.Ready,
                    TranscriptId = transcript.Id,
                    ConsultationId = transcript.ConsultationId,
                    CorrelationId = correlationId,
                    OccurredAtUtc = DateTime.UtcNow,
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Failed to publish {EventType} for transcript {TranscriptId}: {Error}",
                TranscriptReadyEventTypes.Ready,
                transcript.Id,
                ex.Message);
        }

        var dto = _mapper.Map<TranscriptDto>(transcript);
        dto.Status = transcript.Status.ToString();
        return dto;
    }
}
