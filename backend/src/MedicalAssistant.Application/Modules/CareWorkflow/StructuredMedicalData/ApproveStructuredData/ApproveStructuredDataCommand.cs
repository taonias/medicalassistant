using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Contracts.Identity;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Domain;
using MediatR;

namespace MedicalAssistant.Application.Features.MedicalStructuredData.Command.ApproveStructuredData;

public record ApproveStructuredDataCommand(int ConsultationId)
    : IRequest<Unit>, IAuditableRequest<Unit>
{
    public AuditEntry ToAuditEntry(Unit response) =>
        new("ApproveStructuredData", "Consultation", ConsultationId.ToString());
}

public class ApproveStructuredDataCommandHandler : IRequestHandler<ApproveStructuredDataCommand, Unit>
{
    private readonly IUserService _userService;
    private readonly IConsultationStructuredDataApproval _consultationRepository;
    private readonly IMedicalStructuredDataRepository _structuredDataRepository;

    public ApproveStructuredDataCommandHandler(
        IUserService userService,
        IConsultationStructuredDataApproval consultationRepository,
        IMedicalStructuredDataRepository structuredDataRepository)
    {
        _userService = userService;
        _consultationRepository = consultationRepository;
        _structuredDataRepository = structuredDataRepository;
    }

    public async Task<Unit> Handle(ApproveStructuredDataCommand request, CancellationToken cancellationToken)
    {
        var doctorId = await _userService.GetCurrentUserIdAsync()
            ?? throw new BadRequestException("User not authenticated");

        var consultation = await _consultationRepository.GetConsultationForDoctorAsync(request.ConsultationId, doctorId)
            ?? throw new NotFoundException(nameof(Consultation), request.ConsultationId);

        var structured = await _structuredDataRepository.GetLatestByConsultationIdAsync(request.ConsultationId)
            ?? throw new NotFoundException(nameof(MedicalStructuredData), request.ConsultationId);

        structured.Approved = true;
        await _structuredDataRepository.UpdateAsync(structured);

        consultation.MarkCompleted();
        await _consultationRepository.UpdateAsync(consultation);

        return Unit.Value;
    }
}
