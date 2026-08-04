using MedicalAssistant.Domain.Enums;

namespace MedicalAssistant.Domain;

public class ConsultationInboxMessage
{
    public long Id { get; set; }
    public required string ConsumerName { get; set; }
    public Guid EventId { get; set; }
    public required string EventType { get; set; }
    public int EventVersion { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public ConsultationEventMessageStatus Status { get; set; } = ConsultationEventMessageStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public string? LastFailureCategory { get; set; }
    public string? LastFailureCode { get; set; }
}
