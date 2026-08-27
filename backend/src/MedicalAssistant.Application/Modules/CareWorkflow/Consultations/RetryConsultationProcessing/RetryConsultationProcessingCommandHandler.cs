using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Modules.CareWorkflow.Consultations.GetConsultationDetails;
using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Consultations.RetryConsultationProcessing;

public sealed class RetryConsultationProcessingCommandHandler
    : IRequestHandler<RetryConsultationProcessingCommand, ConsultationDto>
{
    private readonly IConsultationRetryStore _retryStore;
    private readonly IConsultationAccess _consultationRepository;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public RetryConsultationProcessingCommandHandler(
        IConsultationRetryStore retryStore,
        IConsultationAccess consultationRepository,
        IUserService userService,
        IMapper mapper)
    {
        _retryStore = retryStore;
        _consultationRepository = consultationRepository;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<ConsultationDto> Handle(
        RetryConsultationProcessingCommand request,
        CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        await _retryStore.RequeueAsync(request.ConsultationId, doctorId, cancellationToken);

        var consultation = await _consultationRepository
            .GetConsultationForDoctorAsync(request.ConsultationId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Consultation), request.ConsultationId);

        return _mapper.Map<ConsultationDto>(consultation);
    }
}
