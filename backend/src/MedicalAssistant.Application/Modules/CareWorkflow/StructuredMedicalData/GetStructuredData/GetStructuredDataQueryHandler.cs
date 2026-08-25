using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MediatR;

namespace MedicalAssistant.Application.Features.MedicalStructuredData.Queries.GetStructuredData;

public class GetStructuredDataQueryHandler : IRequestHandler<GetStructuredDataQuery, MedicalStructuredDataDto?>
{
    private readonly IMedicalStructuredDataRepository _structuredDataRepository;
    private readonly IConsultationAccess _consultationRepository;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public GetStructuredDataQueryHandler(
        IMedicalStructuredDataRepository structuredDataRepository,
        IConsultationAccess consultationRepository,
        IUserService userService,
        IMapper mapper)
    {
        _structuredDataRepository = structuredDataRepository;
        _consultationRepository = consultationRepository;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<MedicalStructuredDataDto?> Handle(GetStructuredDataQuery request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        _ = await _consultationRepository.GetConsultationForDoctorAsync(request.ConsultationId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Consultation), request.ConsultationId);

        var data = await _structuredDataRepository.GetLatestByConsultationIdAsync(request.ConsultationId);
        return data == null ? null : _mapper.Map<MedicalStructuredDataDto>(data);
    }
}
