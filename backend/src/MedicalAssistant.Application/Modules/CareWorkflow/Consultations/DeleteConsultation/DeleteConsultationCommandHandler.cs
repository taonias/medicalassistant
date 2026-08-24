using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Application.Services;
using MedicalAssistant.Domain;
using MediatR;

namespace MedicalAssistant.Application.Features.Consultation.Command.DeleteConsultation;

public class DeleteConsultationCommandHandler : IRequestHandler<DeleteConsultationCommand, Unit>
{
    private readonly IConsultationRepository _consultationRepository;
    private readonly IUserService _userService;

    public DeleteConsultationCommandHandler(
        IConsultationRepository consultationRepository,
        IUserService userService)
    {
        _consultationRepository = consultationRepository;
        _userService = userService;
    }

    public async Task<Unit> Handle(DeleteConsultationCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var consultation = await _consultationRepository.GetConsultationForDoctorAsync(request.Id, doctorId)
            ?? throw new NotFoundException(nameof(Domain.Consultation), request.Id);

        consultation.MarkDeleted(doctorId, "doctor-delete");
        var correlationId = Guid.NewGuid().ToString("N");
        var outboxMessage = ConsultationOutboxFactory.Deleted(consultation, correlationId);
        var cleanup = new ConsultationDeletionCleanup
        {
            ConsultationId = consultation.Id,
            DeletionEventId = outboxMessage.EventId,
            DeletedAtUtc = consultation.DeletedAtUtc!.Value
        };

        await _consultationRepository.RecordDeletionAsync(consultation, cleanup, outboxMessage, cancellationToken);
        return Unit.Value;
    }
}
