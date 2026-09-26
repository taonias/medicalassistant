using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Notifications;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MediatR;

namespace MedicalAssistant.Application.Features.MedicalStructuredData.Command.ProcessStructuredDataCallback;

public class ProcessStructuredDataCallbackCommandHandler : IRequestHandler<ProcessStructuredDataCallbackCommand, Unit>
{
    private readonly IMedicalStructuredDataRepository _structuredDataRepository;
    private readonly IConsultationRepository _consultationRepository;
    private readonly IMediator _mediator;

    public ProcessStructuredDataCallbackCommandHandler(
        IMedicalStructuredDataRepository structuredDataRepository,
        IConsultationRepository consultationRepository,
        IMediator mediator)
    {
        _structuredDataRepository = structuredDataRepository;
        _consultationRepository = consultationRepository;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(ProcessStructuredDataCallbackCommand request, CancellationToken cancellationToken)
    {
        var consultation = await _consultationRepository.GetByIdAsync(request.ConsultationId);

        if (!string.Equals(request.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(request.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                consultation.MarkFailed(request.FailureReason ?? "Structured data extraction failed");
                await _consultationRepository.UpdateAsync(consultation);
            }

            return Unit.Value;
        }

        var existing = await _structuredDataRepository.GetLatestByConsultationIdAsync(request.ConsultationId);
        if (existing == null)
        {
            var entity = new Domain.MedicalStructuredData
            {
                ConsultationId = request.ConsultationId,
                TranscriptId = request.TranscriptId,
                SchemaVersion = request.SchemaVersion,
                StructuredPayload = request.StructuredPayload,
                Approved = false,
                ExtractedAt = DateTime.UtcNow
            };

            await _structuredDataRepository.CreateAsync(entity);
            await _mediator.Publish(
                new StructuredDataPersistedNotification { ConsultationId = request.ConsultationId },
                cancellationToken);
        }

        if (consultation.Status is ConsultationStatus.Transcribing or ConsultationStatus.Transcribed)
        {
            consultation.MarkStructuredDataPending();
            await _consultationRepository.UpdateAsync(consultation);
        }

        return Unit.Value;
    }
}
