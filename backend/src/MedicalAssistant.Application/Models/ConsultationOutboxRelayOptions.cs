namespace MedicalAssistant.Application.Models;

public sealed class ConsultationOutboxRelayOptions
{
    public const string SectionName = "ConsultationOutboxRelay";

    public bool Enabled { get; set; }
    public int BatchSize { get; set; } = 25;
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan FailureBackoff { get; set; } = TimeSpan.FromSeconds(30);
    public string LeaseOwner { get; set; } = $"medicalassistant-outbox-{Environment.MachineName}";
}
