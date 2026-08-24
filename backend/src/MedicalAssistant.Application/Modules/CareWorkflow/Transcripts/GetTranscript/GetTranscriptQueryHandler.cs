using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MediatR;

namespace MedicalAssistant.Application.Features.Transcript.Queries.GetTranscript;

public class GetTranscriptQueryHandler : IRequestHandler<GetTranscriptQuery, TranscriptDto?>
{
    private readonly ITranscriptRepository _transcriptRepository;
    private readonly IConsultationRepository _consultationRepository;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public GetTranscriptQueryHandler(
        ITranscriptRepository transcriptRepository,
        IConsultationRepository consultationRepository,
        IUserService userService,
        IMapper mapper)
    {
        _transcriptRepository = transcriptRepository;
        _consultationRepository = consultationRepository;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<TranscriptDto?> Handle(GetTranscriptQuery request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        _ = await _consultationRepository.GetConsultationForDoctorAsync(request.ConsultationId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Consultation), request.ConsultationId);

        var transcript = await _transcriptRepository.GetByConsultationIdAsync(request.ConsultationId);
        if (transcript == null) return null;

        var dto = _mapper.Map<TranscriptDto>(transcript);
        dto.Status = transcript.Status.ToString();
        return dto;
    }
}
