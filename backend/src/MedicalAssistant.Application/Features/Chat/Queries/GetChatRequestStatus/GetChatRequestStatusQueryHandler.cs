using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MediatR;

namespace MedicalAssistant.Application.Features.Chat.Queries.GetChatRequestStatus;

public class GetChatRequestStatusQueryHandler : IRequestHandler<GetChatRequestStatusQuery, ChatJobDto>
{
    private readonly IChatRequestRepository _chatRequestRepository;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;

    public GetChatRequestStatusQueryHandler(
        IChatRequestRepository chatRequestRepository,
        IUserService userService,
        IMapper mapper)
    {
        _chatRequestRepository = chatRequestRepository;
        _userService = userService;
        _mapper = mapper;
    }

    public async Task<ChatJobDto> Handle(GetChatRequestStatusQuery request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var chatRequest = await _chatRequestRepository.GetByCorrelationIdForDoctorAsync(request.CorrelationId, doctorId)
            ?? throw new NotFoundException(nameof(Domain.ChatRequest), request.CorrelationId);

        return _mapper.Map<ChatJobDto>(chatRequest);
    }
}
