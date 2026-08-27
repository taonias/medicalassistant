using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Notifications;
using MedicalAssistant.Domain;
using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.StructuredMedicalData.ProcessStructuredDataCallback;

public class ProcessStructuredDataCallbackCommandHandler : IRequestHandler<ProcessStructuredDataCallbackCommand, Unit>
{
    private readonly IMedicalStructuredDataRepository _structuredDataRepository;
    private readonly IStructuredDataCompletion _consultationRepository;
    private readonly IMediator _mediator;

    public ProcessStructuredDataCallbackCommandHandler(
        IMedicalStructuredDataRepository structuredDataRepository,
        IStructuredDataCompletion consultationRepository,
        IMediator mediator)
    {
        _structuredDataRepository = structuredDataRepository;
        _consultationRepository = consultationRepository;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(ProcessStructuredDataCallbackCommand request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(request.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                var consultation = await _consultationRepository.GetByIdAsync(request.ConsultationId);
                consultation.MarkFailed(request.FailureReason ?? "Structured data extraction failed");
                await _consultationRepository.UpdateAsync(consultation);
            }

            return Unit.Value;
        }

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

        // Keep consultation in StructuredDataPending until the doctor approves.
        // The existing TranscriptionCompletedNotification already sets this state.

        // Notify background indexing so the AI module can answer patient-scoped questions with citations.
        await _mediator.Publish(new StructuredDataPersistedNotification { ConsultationId = request.ConsultationId }, cancellationToken);

        return Unit.Value;
    }
}
