using MedicalAssistant.Application.Contracts.AiModule;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Models.AiModule;
using MediatR;
using System.Text.Json;

namespace MedicalAssistant.Application.Features.Chat.Queries.ChatQuery;

public class ChatQueryHandler : IRequestHandler<ChatQuery, ChatResponseDto>
{
    private readonly IAiModuleClient _aiModuleClient;
    private readonly IUserService _userService;
    private readonly IPatientRepository _patientRepository;
    private readonly IConsultationRepository _consultationRepository;
    private readonly ITranscriptRepository _transcriptRepository;
    private readonly IMedicalStructuredDataRepository _structuredDataRepository;
    private readonly IDoctorNoteRepository _doctorNoteRepository;

    public ChatQueryHandler(
        IAiModuleClient aiModuleClient,
        IUserService userService,
        IPatientRepository patientRepository,
        IConsultationRepository consultationRepository,
        ITranscriptRepository transcriptRepository,
        IMedicalStructuredDataRepository structuredDataRepository,
        IDoctorNoteRepository doctorNoteRepository)
    {
        _aiModuleClient = aiModuleClient;
        _userService = userService;
        _patientRepository = patientRepository;
        _consultationRepository = consultationRepository;
        _transcriptRepository = transcriptRepository;
        _structuredDataRepository = structuredDataRepository;
        _doctorNoteRepository = doctorNoteRepository;
    }

    public async Task<ChatResponseDto> Handle(ChatQuery request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var context = await BuildContextAsync(request, doctorId, cancellationToken);

        var response = await _aiModuleClient.ChatAsync(new ChatRequest
        {
            Message = request.Message,
            ContextJson = JsonSerializer.Serialize(context),
            SessionId = request.SessionId
        }, cancellationToken);

        return new ChatResponseDto
        {
            Answer = response.Answer,
            Citations = response.Citations,
            SuggestedActions = response.SuggestedActions
        };
    }

    private async Task<object> BuildContextAsync(ChatQuery request, string doctorId, CancellationToken cancellationToken)
    {
        if (request.ConsultationId.HasValue)
        {
            var consultation = await _consultationRepository.GetConsultationForDoctorAsync(request.ConsultationId.Value, doctorId)
                ?? throw new NotFoundException(nameof(Domain.Consultation), request.ConsultationId.Value);

            var transcript = await _transcriptRepository.GetByConsultationIdAsync(consultation.Id);
            var structured = await _structuredDataRepository.GetLatestByConsultationIdAsync(consultation.Id);
            var doctorNotes = await _doctorNoteRepository.GetByConsultationIdForDoctorAsync(consultation.Id, doctorId);

            return new
            {
                consultation.Id,
                consultation.PatientId,
                consultation.ConsultationDate,
                consultation.Status,
                Transcript = transcript?.TranscriptText,
                StructuredData = structured?.StructuredPayload,
                DoctorNotes = doctorNotes.Select(n => new { n.Id, n.Content })
            };
        }

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
