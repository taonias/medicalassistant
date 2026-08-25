using MedicalAssistant.Application.Contracts.Persistence;
using MediatR;

namespace MedicalAssistant.Application.Notifications;

public class TranscriptionCompletedNotificationHandler : INotificationHandler<TranscriptionCompletedNotification>
{
    private readonly ITranscriptionCompletion _consultationRepository;

    public TranscriptionCompletedNotificationHandler(ITranscriptionCompletion consultationRepository)
    {
        _consultationRepository = consultationRepository;
    }

    public async Task Handle(TranscriptionCompletedNotification notification, CancellationToken cancellationToken)
    {
        var consultation = await _consultationRepository.GetByIdAsync(notification.ConsultationId);
        consultation.MarkStructuredDataPending();
        await _consultationRepository.UpdateAsync(consultation);
    }
}
