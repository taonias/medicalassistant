using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MediatR;

namespace MedicalAssistant.Application.Features.Consultation.Queries.GetConsultationDetails;

public class GetConsultationDetailsQueryHandler : IRequestHandler<GetConsultationDetailsQuery, ConsultationDto>
{
    private readonly IConsultationRepository _consultationRepository;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public GetConsultationDetailsQueryHandler(
        IConsultationRepository consultationRepository,
        IUserService userService,
        IMapper mapper)
    {
        _consultationRepository = consultationRepository;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<ConsultationDto> Handle(GetConsultationDetailsQuery request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var consultation = await _consultationRepository.GetConsultationForDoctorAsync(request.Id, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Consultation), request.Id);

        var dto = _mapper.Map<ConsultationDto>(consultation);
        dto.Status = consultation.Status.ToString();
        return dto;
    }
}
