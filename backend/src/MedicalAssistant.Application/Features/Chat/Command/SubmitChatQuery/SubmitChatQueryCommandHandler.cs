using AutoMapper;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Features.Chat.Queries.GetChatRequestStatus;
using MedicalAssistant.Application.Models.Messaging;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MediatR;
using System.Text.Json;

namespace MedicalAssistant.Application.Features.Chat.Command.SubmitChatQuery;

public class SubmitChatQueryCommandHandler : IRequestHandler<SubmitChatQueryCommand, ChatJobDto>
{
    private readonly IAiRequestPublisher _aiRequestPublisher;
    private readonly IUserService _userService;
    private readonly IPatientRepository _patientRepository;
    private readonly IConsultationRepository _consultationRepository;
    private readonly IChatRequestRepository _chatRequestRepository;
    private readonly IMapper _mapper;

    public SubmitChatQueryCommandHandler(
        IAiRequestPublisher aiRequestPublisher,
        IUserService userService,
        IPatientRepository patientRepository,
        IConsultationRepository consultationRepository,
        IChatRequestRepository chatRequestRepository,
        IMapper mapper)
    {
        _aiRequestPublisher = aiRequestPublisher;
        _userService = userService;
        _patientRepository = patientRepository;
        _consultationRepository = consultationRepository;
        _chatRequestRepository = chatRequestRepository;
        _mapper = mapper;
    }

    public async Task<ChatJobDto> Handle(SubmitChatQueryCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var context = await BuildContextAsync(request, doctorId);
        var contextJson = JsonSerializer.Serialize(context);
        var correlationId = Guid.NewGuid().ToString("N");

        var chatRequest = new ChatRequest
        {
            CorrelationId = correlationId,
            DoctorId = doctorId,
            PatientId = request.PatientId,
            Message = request.Message,
            SessionId = request.SessionId,
            ContextJson = contextJson,
            Status = ChatRequestStatus.Pending
        };

        await _chatRequestRepository.CreateAsync(chatRequest);

        try
        {
            await _aiRequestPublisher.PublishChatAsync(new ChatRequestedMessage
            {
                EventType = AiRequestEventTypes.ChatRequested,
                ChatRequestId = chatRequest.Id,
                DoctorId = doctorId,
                PatientId = request.PatientId,
                Message = request.Message,
                ContextJson = contextJson,
                SessionId = request.SessionId,
                CorrelationId = correlationId,
                OccurredAtUtc = DateTime.UtcNow
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            chatRequest.MarkFailed(ex.Message);
            await _chatRequestRepository.UpdateAsync(chatRequest);
            throw;
        }

        return _mapper.Map<ChatJobDto>(chatRequest);
    }

    private async Task<object> BuildContextAsync(SubmitChatQueryCommand request, string doctorId)
    {
        if (request.PatientId.HasValue)
        {
            var patient = await _patientRepository.GetPatientForDoctorAsync(request.PatientId.Value, doctorId)
                ?? throw new NotFoundException(nameof(Domain.Patient), request.PatientId.Value);

            var consultations = await _consultationRepository.GetConsultationsByPatientForDoctorAsync(patient.Id, doctorId);

            return new
            {
                Patient = new { patient.Id, patient.FirstName, patient.LastName, patient.DateOfBirth },
                Consultations = consultations.Select(c => new { c.Id, c.ConsultationDate, c.Status })
            };
        }

        return new { Mode = "general" };
    }
}
