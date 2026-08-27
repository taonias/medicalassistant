using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MediatR;

namespace MedicalAssistant.Application.Modules.Integrations.LegacyAiModule.GetActionRequestStatus;

public class GetActionRequestStatusQueryHandler : IRequestHandler<GetActionRequestStatusQuery, ActionRequestDto>
{
    private readonly IActionRequestRepository _actionRequestRepository;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public GetActionRequestStatusQueryHandler(
        IActionRequestRepository actionRequestRepository,
        IUserService userService,
        IMapper mapper)
    {
        _actionRequestRepository = actionRequestRepository;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<ActionRequestDto> Handle(GetActionRequestStatusQuery request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var actionRequest = await _actionRequestRepository.GetByCorrelationIdForDoctorAsync(request.CorrelationId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.ActionRequest), request.CorrelationId);

        var dto = _mapper.Map<ActionRequestDto>(actionRequest);
        dto.Status = actionRequest.Status.ToString();
        dto.ActionType = actionRequest.ActionType.ToString();
        return dto;
    }
}
