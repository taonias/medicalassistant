using MedicalAssistant.Application.Contracts.AiModule;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Models.AiModule;
using MedicalAssistant.Domain;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Application.Features.DoctorNotes.Command.CreateDoctorNote;

public class CreateDoctorNoteCommand : IRequest<DoctorNote>
{
    public required int ConsultationId { get; set; }
    public string? Title { get; set; }
    public required string Content { get; set; }
}

public class CreateDoctorNoteCommandHandler : IRequestHandler<CreateDoctorNoteCommand, DoctorNote>
{
    private readonly IDoctorNoteRepository _doctorNoteRepository;
    private readonly IConsultationRepository _consultationRepository;
    private readonly IUserService _userService;
    private readonly IAiModuleClient _aiModuleClient;
    private readonly ILogger<CreateDoctorNoteCommandHandler> _logger;

    public CreateDoctorNoteCommandHandler(
        IDoctorNoteRepository doctorNoteRepository,
        IConsultationRepository consultationRepository,
        IUserService userService,
        IAiModuleClient aiModuleClient,
        ILogger<CreateDoctorNoteCommandHandler> logger)
    {
        _doctorNoteRepository = doctorNoteRepository;
        _consultationRepository = consultationRepository;
        _userService = userService;
        _aiModuleClient = aiModuleClient;
        _logger = logger;
    }

    public async Task<DoctorNote> Handle(CreateDoctorNoteCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var consultation = await _consultationRepository.GetConsultationForDoctorAsync(request.ConsultationId, doctorId);
        if (consultation == null)
            throw new NotFoundException(nameof(Consultation), request.ConsultationId);

        if (!consultation.PatientId.HasValue)
            throw new BadRequestException("Cannot create doctor notes for a consultation without an assigned patient.");

        var note = new DoctorNote
        {
            DoctorId = doctorId,
            PatientId = consultation.PatientId.Value,
            ConsultationId = consultation.Id,
            Title = request.Title,
            Content = request.Content
        };

        await _doctorNoteRepository.CreateAsync(note);

        // Best-effort indexing for patient-scoped RAG.
        try
        {
            await _aiModuleClient.IndexDocumentsAsync(new IndexDocumentsJobRequest
            {
                Documents =
                [
                    new IndexDocument
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
            _logger.LogError(ex, "Failed to index doctor note {NoteId}", note.Id);
        }

        return note;
    }
}
