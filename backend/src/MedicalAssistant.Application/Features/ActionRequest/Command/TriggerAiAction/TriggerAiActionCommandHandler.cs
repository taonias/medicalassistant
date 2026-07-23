using AutoMapper;
using MedicalAssistant.Application.Contracts.AiModule;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Features.ActionRequest.Queries.GetActionRequestStatus;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Models.AiModule;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Application.Features.ActionRequest.Command.TriggerAiAction;

public class TriggerAiActionCommandHandler : IRequestHandler<TriggerAiActionCommand, ActionRequestDto>
{
    private readonly IActionRequestRepository _actionRequestRepository;
    private readonly IConsultationRepository _consultationRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IAiModuleClient _aiModuleClient;
    private readonly IUserService _userService;
    private readonly IMapper _mapper;
    private readonly AiModuleSettings _aiSettings;

    public TriggerAiActionCommandHandler(
        IActionRequestRepository actionRequestRepository,
        IConsultationRepository consultationRepository,
        IPatientRepository patientRepository,
        IAiModuleClient aiModuleClient,
        IUserService userService,
        IMapper mapper,
        IOptions<AiModuleSettings> aiSettings)
    {
        _actionRequestRepository = actionRequestRepository;
        _consultationRepository = consultationRepository;
        _patientRepository = patientRepository;
        _aiModuleClient = aiModuleClient;
        _userService = userService;
        _mapper = mapper;
        _aiSettings = aiSettings.Value;
    }

    public async Task<ActionRequestDto> Handle(TriggerAiActionCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : request.CorrelationId;

        var existing = await _actionRequestRepository.GetByCorrelationIdAsync(correlationId);
        if (existing != null)
        {
            if (!string.Equals(existing.DoctorId, doctorId, StringComparison.Ordinal))
                throw new BadRequestException("Correlation ID belongs to another doctor.");

            var existingDto = _mapper.Map<ActionRequestDto>(existing);
            existingDto.Status = existing.Status.ToString();
            existingDto.ActionType = existing.ActionType.ToString();
            return existingDto;
        }

        if (request.PatientId.HasValue)
        {
            _ = await _patientRepository.GetPatientForDoctorAsync(request.PatientId.Value, doctorId)
                ?? throw new NotFoundException(nameof(Domain.Patient), request.PatientId.Value);
        }

        if (request.ConsultationId.HasValue)
        {
            _ = await _consultationRepository.GetConsultationForDoctorAsync(request.ConsultationId.Value, doctorId)
                ?? throw new NotFoundException(nameof(Domain.Consultation), request.ConsultationId.Value);
        }

        var actionRequest = new Domain.ActionRequest
        {
            CorrelationId = correlationId,
            DoctorId = doctorId,
            PatientId = request.PatientId,
            ConsultationId = request.ConsultationId,
            ActionType = request.ActionType,
            Status = ActionRequestStatus.Pending,
            RequestPayload = request.ParametersJson
        };

        await _actionRequestRepository.CreateAsync(actionRequest);

        var callbackUrl = $"{_aiSettings.ApiBaseUrl.TrimEnd('/')}/api/ai-callback/action";
        var aiResponse = await _aiModuleClient.TriggerActionAsync(new ActionJobRequest
        {
            ActionType = request.ActionType.ToString(),
            PatientId = request.PatientId,
            ConsultationId = request.ConsultationId,
            CorrelationId = correlationId,
            CallbackUrl = callbackUrl,
            ParametersJson = request.ParametersJson
        }, cancellationToken);

        actionRequest.MarkInProgress(aiResponse.JobId);
        await _actionRequestRepository.UpdateAsync(actionRequest);

        var dto = _mapper.Map<ActionRequestDto>(actionRequest);
        dto.Status = actionRequest.Status.ToString();
        dto.ActionType = actionRequest.ActionType.ToString();
        return dto;
    }
}
