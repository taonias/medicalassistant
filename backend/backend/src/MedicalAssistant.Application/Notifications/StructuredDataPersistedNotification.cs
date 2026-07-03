using MediatR;

namespace MedicalAssistant.Application.Notifications;

public class StructuredDataPersistedNotification : INotification
{
    public int ConsultationId { get; init; }
}
