using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Models.Messaging;
using MedicalAssistant.Domain;
using MediatR;

namespace MedicalAssistant.Application.Features.DoctorNotes.Command.CreateDoctorNote;

public class CreateDoctorNoteCommand : IRequest<DoctorNote>
{
    public int? ConsultationId { get; set; }
    public int? PatientId { get; set; }
    public required string Content { get; set; }
}

public class CreateDoctorNoteCommandHandler : IRequestHandler<CreateDoctorNoteCommand, DoctorNote>
{
    private readonly IDoctorNoteRepository _doctorNoteRepository;
    private readonly IConsultationRepository _consultationRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IUserService _userService;
    private readonly IAiRequestPublisher _aiRequestPublisher;
    private readonly IAppLogger<CreateDoctorNoteCommandHandler> _logger;

    public CreateDoctorNoteCommandHandler(
        IDoctorNoteRepository doctorNoteRepository,
        IConsultationRepository consultationRepository,
        IPatientRepository patientRepository,
        IUserService userService,
        IAiRequestPublisher aiRequestPublisher,
        IAppLogger<CreateDoctorNoteCommandHandler> logger)
    {
        _doctorNoteRepository = doctorNoteRepository;
        _consultationRepository = consultationRepository;
        _patientRepository = patientRepository;
        _userService = userService;
        _aiRequestPublisher = aiRequestPublisher;
        _logger = logger;
    }

    public async Task<DoctorNote> Handle(CreateDoctorNoteCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        int patientId;
        int? consultationId;

        if (request.ConsultationId is int consultationIdValue)
        {
            var consultation = await _consultationRepository.GetConsultationForDoctorAsync(consultationIdValue, doctorId);
            if (consultation == null)
                throw new NotFoundException(nameof(Consultation), consultationIdValue);

            if (!consultation.PatientId.HasValue)
                throw new BadRequestException("Cannot create doctor notes for a consultation without an assigned patient.");

            patientId = consultation.PatientId.Value;
            consultationId = consultation.Id;
        }
        else
        {
            if (request.PatientId is not int patientIdValue)
                throw new BadRequestException("PatientId is required when ConsultationId is not provided.");

            var patient = await _patientRepository.GetPatientForDoctorAsync(patientIdValue, doctorId);
            if (patient == null)
                throw new NotFoundException(nameof(Patient), patientIdValue);

            patientId = patient.Id;
            consultationId = null;
        }

        var note = new DoctorNote
        {
            DoctorId = doctorId,
            PatientId = patientId,
            ConsultationId = consultationId,
            Content = request.Content
        };

        await _doctorNoteRepository.CreateAsync(note);

        try
        {
            await _aiRequestPublisher.PublishIndexAsync(new IndexDocumentsRequestedMessage
            {
                EventType = AiRequestEventTypes.DocumentsIndexRequested,
                CorrelationId = Guid.NewGuid().ToString("N"),
                OccurredAtUtc = DateTime.UtcNow,
                Documents =
                [
                    new IndexDocumentMessage
                    {
                        Id = $"note:{note.Id}",
                        PatientId = note.PatientId,
                        ConsultationId = note.ConsultationId,
                        DocType = "note",
                        Content = note.Content
                    }
                ]
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to queue indexing for doctor note {NoteId}: {Error}", note.Id, ex.Message);
        }

        return note;
    }
}
