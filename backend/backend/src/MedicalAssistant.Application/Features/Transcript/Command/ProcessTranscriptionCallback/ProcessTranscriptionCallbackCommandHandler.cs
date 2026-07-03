using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Notifications;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MediatR;

namespace MedicalAssistant.Application.Features.Transcript.Command.ProcessTranscriptionCallback;

public class ProcessTranscriptionCallbackCommandHandler : IRequestHandler<ProcessTranscriptionCallbackCommand, Unit>
{
    private readonly ITranscriptRepository _transcriptRepository;
    private readonly IConsultationRepository _consultationRepository;
    private readonly IMediator _mediator;

    public ProcessTranscriptionCallbackCommandHandler(
        ITranscriptRepository transcriptRepository,
        IConsultationRepository consultationRepository,
        IMediator mediator)
    {
        _transcriptRepository = transcriptRepository;
        _consultationRepository = consultationRepository;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(ProcessTranscriptionCallbackCommand request, CancellationToken cancellationToken)
    {
        var transcript = await _transcriptRepository.GetByExternalJobIdAsync(request.JobId)
            ?? await _transcriptRepository.GetByConsultationIdAsync(request.ConsultationId);

        if (transcript == null)
        {
            transcript = new Domain.Transcript
            {
                ConsultationId = request.ConsultationId,
                ExternalJobId = request.JobId,
                Status = TranscriptStatus.Processing
            };
            await _transcriptRepository.CreateAsync(transcript);
        }

        if (transcript.Status == TranscriptStatus.Completed)
            return Unit.Value;

        var consultation = await _consultationRepository.GetByIdAsync(request.ConsultationId);

        if (string.Equals(request.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            transcript.MarkCompleted(request.RawText ?? string.Empty, request.TranscriptBlobUri);
            await _transcriptRepository.UpdateAsync(transcript);

            consultation.MarkTranscribed();
            await _consultationRepository.UpdateAsync(consultation);

            await _mediator.Publish(new TranscriptionCompletedNotification
            {
                ConsultationId = consultation.Id,
                TranscriptId = transcript.Id,
                TranscriptText = transcript.RawText ?? string.Empty,
                CorrelationId = request.CorrelationId
            }, cancellationToken);
        }
        else if (string.Equals(request.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            transcript.MarkFailed(request.FailureReason ?? "Transcription failed");
            await _transcriptRepository.UpdateAsync(transcript);

            consultation.MarkFailed(request.FailureReason ?? "Transcription failed");
            await _consultationRepository.UpdateAsync(consultation);
        }

        return Unit.Value;
    }
}
