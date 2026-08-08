using System.Globalization;
using MedicalAssistant.Application.Contracts.ClinicalKnowledge;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MediatR;

namespace MedicalAssistant.Application.Features.Chat.Queries.ChatQuery;

public class ChatQueryHandler : IRequestHandler<ChatQuery, ChatResponseDto>
{
    private readonly IClinicalKnowledgeClient _clinicalKnowledge;
    private readonly IUserService _userService;
    private readonly IPatientRepository _patientRepository;
    private readonly IConsultationRepository _consultationRepository;

    public ChatQueryHandler(
        IClinicalKnowledgeClient clinicalKnowledge,
        IUserService userService,
        IPatientRepository patientRepository,
        IConsultationRepository consultationRepository)
    {
        _clinicalKnowledge = clinicalKnowledge;
        _userService = userService;
        _patientRepository = patientRepository;
        _consultationRepository = consultationRepository;
    }

    public async Task<ChatResponseDto> Handle(ChatQuery request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        // Clinical Knowledge answers are patient-scoped RAG over the ingested record.
        var patient = await ResolvePatientAsync(request, doctorId);
        if (patient is null)
        {
            return new ChatResponseDto
            {
                Answer = "Select a patient to ask about their clinical record."
            };
        }

        // Must match the identifier used at ingestion: external id when present,
        // otherwise the internal id (see ConsultationTranscriptReadyIntegrationEventHandler).
        var clinicalPatientId = string.IsNullOrWhiteSpace(patient.ExternalPatientId)
            ? patient.Id.ToString(CultureInfo.InvariantCulture)
            : patient.ExternalPatientId;

        var answer = await _clinicalKnowledge.GetGroundedAnswerAsync(
            new ClinicalKnowledgeChatRequest(
                PatientId: clinicalPatientId,
                DoctorId: doctorId,
                Question: request.Message),
            cancellationToken);

        return new ChatResponseDto
        {
            Answer = answer.Text,
            Citations = answer.Citations
                .Select(citation => string.IsNullOrWhiteSpace(citation.Label)
                    ? citation.Quote
                    : $"[{citation.Label}] {citation.Quote}")
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList()
        };
    }

    private async Task<Domain.Patient?> ResolvePatientAsync(ChatQuery request, string doctorId)
    {
        if (request.ConsultationId.HasValue)
        {
            var consultation = await _consultationRepository.GetConsultationForDoctorAsync(request.ConsultationId.Value, doctorId)
                ?? throw new NotFoundException(nameof(Domain.Consultation), request.ConsultationId.Value);

            if (!consultation.PatientId.HasValue)
                return null;

            return await _patientRepository.GetPatientForDoctorAsync(consultation.PatientId.Value, doctorId)
                ?? throw new NotFoundException(nameof(Domain.Patient), consultation.PatientId.Value);
        }

        if (request.PatientId.HasValue)
        {
            return await _patientRepository.GetPatientForDoctorAsync(request.PatientId.Value, doctorId)
                ?? throw new NotFoundException(nameof(Domain.Patient), request.PatientId.Value);
        }

        return null;
    }
}
