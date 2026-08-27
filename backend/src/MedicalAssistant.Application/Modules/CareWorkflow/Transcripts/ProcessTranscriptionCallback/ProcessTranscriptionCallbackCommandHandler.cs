using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain;
using MedicalAssistant.Domain.Enums;
using MediatR;

namespace MedicalAssistant.Application.Modules.CareWorkflow.Transcripts.ProcessTranscriptionCallback;

public class ProcessTranscriptionCallbackCommandHandler : IRequestHandler<ProcessTranscriptionCallbackCommand, Unit>
{
    private readonly ITranscriptRepository _transcriptRepository;
    private readonly ITranscriptionCompletion _consultationRepository;

    public ProcessTranscriptionCallbackCommandHandler(
        ITranscriptRepository transcriptRepository,
        ITranscriptionCompletion consultationRepository)
    {
        _transcriptRepository = transcriptRepository;
        _consultationRepository = consultationRepository;
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
            transcript.MarkCompleted(request.Transcript ?? string.Empty);
            consultation.MarkTranscribed();
            await _transcriptRepository.CompleteWithOutboxAsync(
                transcript,
                consultation,
                request.CorrelationId,
                cancellationToken);
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
