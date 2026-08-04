using System.Diagnostics.Metrics;

namespace MedicalAssistant.Application.Services;

public sealed class ConsultationOutboxRelayMetrics : IConsultationOutboxRelayObserver, IDisposable
{
    public const string MeterName = "MedicalAssistant.ConsultationOutbox";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _claimed;
    private readonly Counter<long> _published;
    private readonly Counter<long> _failed;

    public ConsultationOutboxRelayMetrics()
    {
        _claimed = _meter.CreateCounter<long>(
            "medicalassistant.outbox.messages.claimed",
            unit: "{message}",
            description: "Outbox messages claimed for publication.");
        _published = _meter.CreateCounter<long>(
            "medicalassistant.outbox.messages.published",
            unit: "{message}",
            description: "Outbox messages published after broker confirmation.");
        _failed = _meter.CreateCounter<long>(
            "medicalassistant.outbox.messages.failed",
            unit: "{message}",
            description: "Outbox messages scheduled for retry after publication failure.");
    }

    public void BatchClaimed(int messageCount) => _claimed.Add(messageCount);

    public void MessagePublished(string eventType) =>
        _published.Add(1, new KeyValuePair<string, object?>("event.type", eventType));

    public void MessagePublishFailed(string eventType, string failureCategory) =>
        _failed.Add(
            1,
            new KeyValuePair<string, object?>("event.type", eventType),
            new KeyValuePair<string, object?>("failure.category", failureCategory));

    public void Dispose() => _meter.Dispose();
}
