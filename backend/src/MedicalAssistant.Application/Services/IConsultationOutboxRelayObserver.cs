namespace MedicalAssistant.Application.Services;

public interface IConsultationOutboxRelayObserver
{
    void BatchClaimed(int messageCount);

    void MessagePublished(string eventType);

    void MessagePublishFailed(string eventType, string failureCategory);
}
