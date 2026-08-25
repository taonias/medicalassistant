using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Features.Transcript.Queries.GetTranscript;
using MedicalAssistant.Application.Services;
using MediatR;

namespace MedicalAssistant.Application.Features.Transcript.Command.UpdateTranscript;

public class UpdateTranscriptCommandHandler : IRequestHandler<UpdateTranscriptCommand, TranscriptDto>
{
    private readonly ITranscriptRepository _transcriptRepository;
    private readonly IConsultationAccess _consultationRepository;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public UpdateTranscriptCommandHandler(
        ITranscriptRepository transcriptRepository,
        IConsultationAccess consultationRepository,
        IUserService userService,
        IMapper mapper)
    {
        _transcriptRepository = transcriptRepository;
        _consultationRepository = consultationRepository;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<TranscriptDto> Handle(UpdateTranscriptCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var consultation = await _consultationRepository.GetConsultationForDoctorAsync(request.ConsultationId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Consultation), request.ConsultationId);

        var transcript = await _transcriptRepository.GetByConsultationIdAsync(request.ConsultationId)
            ?? throw new NotFoundException(nameof(Domain.Transcript), request.ConsultationId);

        if (string.IsNullOrWhiteSpace(transcript.TranscriptText))
            throw new BadRequestException("Transcript cannot be edited until it has been populated.");

        transcript.UpdateText(request.Transcript);
        var correlationId = Guid.NewGuid().ToString("N");
        var outboxMessage = ConsultationOutboxFactory.TranscriptReady(consultation, transcript, correlationId);
        await _transcriptRepository.UpdateWithOutboxAsync(transcript, consultation, outboxMessage, cancellationToken);

        var dto = _mapper.Map<TranscriptDto>(transcript);
        dto.Status = transcript.Status.ToString();
        return dto;
    }
}
