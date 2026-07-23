using MedicalAssistant.Application.Contracts.Persistence;
using MediatR;

namespace MedicalAssistant.Application.Notifications;

public class TranscriptionCompletedNotificationHandler : INotificationHandler<TranscriptionCompletedNotification>
{
    private readonly IConsultationRepository _consultationRepository;

    public TranscriptionCompletedNotificationHandler(IConsultationRepository consultationRepository)
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
