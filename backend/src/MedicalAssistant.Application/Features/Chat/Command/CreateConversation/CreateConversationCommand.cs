using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Features.Chat.Common;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MediatR;

namespace MedicalAssistant.Application.Features.Chat.Command.CreateConversation;

/// <summary>Explicitly starts a new conversation. The first question renames it (auto-title).</summary>
public sealed class CreateConversationCommand : IRequest<ConversationSummaryDto>, IAuditableRequest<ConversationSummaryDto>
{
    public int? PatientId { get; set; }
    public int? ConsultationId { get; set; }

    public AuditEntry ToAuditEntry(ConversationSummaryDto response) =>
        new("CreateConversation", "Conversation", response.Id.ToString(), $"PatientId: {response.PatientId}");
}

public sealed class CreateConversationCommandHandler
    : IRequestHandler<CreateConversationCommand, ConversationSummaryDto>
{
    private const string PlaceholderTitle = "New conversation";

    private readonly IConversationRepository _conversations;
    private readonly IPatientRepository _patients;
    private readonly IConsultationRepository _consultationRepository;
    private readonly IUserService _userService;

    public CreateConversationCommandHandler(
        IConversationRepository conversations,
        IPatientRepository patients,
        IConsultationRepository consultationRepository,
        IUserService userService)
    {
        _conversations = conversations;
        _patients = patients;
        _consultationRepository = consultationRepository;
        _userService = userService;
    }

    public async Task<ConversationSummaryDto> Handle(
        CreateConversationCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        int patientId;
        int? consultationId = null;

        if (request.ConsultationId is int consultationIdValue)
        {
            var consultation = await _consultationRepository.GetConsultationForDoctorAsync(consultationIdValue, doctorId)
                ?? throw new NotFoundException(nameof(Domain.Consultation), consultationIdValue);

            if (!consultation.PatientId.HasValue)
            {
                throw new BadRequestException("Cannot start a conversation from a consultation without an assigned patient.");
            }

            patientId = consultation.PatientId.Value;
            consultationId = consultation.Id;
        }
        else if (request.PatientId is int patientIdValue)
        {
            var patient = await _patients.GetPatientForDoctorAsync(patientIdValue, doctorId)
                ?? throw new NotFoundException(nameof(Domain.Patient), patientIdValue);

            patientId = patient.Id;
        }
        else
        {
            throw new BadRequestException("A patientId or consultationId is required.");
        }

        var conversation = new Conversation
        {
            DoctorId = doctorId,
            PatientId = patientId,
            ConsultationId = consultationId,
            Title = PlaceholderTitle,
            Status = ConversationStatus.Active,
        };

        await _conversations.CreateAsync(conversation);
        return conversation.ToSummaryDto();
    }
}
