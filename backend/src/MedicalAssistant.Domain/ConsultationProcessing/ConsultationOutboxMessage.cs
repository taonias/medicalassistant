using MedicalAssistant.Domain.Enums;

namespace MedicalAssistant.Domain;

public class ConsultationOutboxMessage
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public required string EventType { get; set; }
    public int EventVersion { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public required string Producer { get; set; }
    public string? CorrelationId { get; set; }
    public string? CausationId { get; set; }
    public string? AggregateType { get; set; }
    public string? AggregateId { get; set; }
    public required string Payload { get; set; }
    public ConsultationEventMessageStatus Status { get; set; } = ConsultationEventMessageStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public string? LastFailureCategory { get; set; }
    public string? LastFailureCode { get; set; }
}
