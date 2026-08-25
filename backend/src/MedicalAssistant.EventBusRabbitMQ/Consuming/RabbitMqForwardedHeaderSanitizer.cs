namespace MedicalAssistant.EventBusRabbitMQ;

public static class RabbitMqForwardedHeaderSanitizer
{
    private static readonly HashSet<string> ForwardedHeaderAllowlist =
    [
        RabbitMqTelemetryHeaders.TraceParent,
        RabbitMqTelemetryHeaders.TraceState
    ];

    public static Dictionary<string, object?> SanitizeForRetryOrDeadLetter(
        IDictionary<string, object?>? sourceHeaders,
        int retryAttempt)
    {
        var sanitized = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (sourceHeaders is not null)
        {
            foreach (var (key, value) in sourceHeaders)
            {
                if (ForwardedHeaderAllowlist.Contains(key))
                {
                    sanitized[key] = value;
                }
            }
        }

        sanitized[RabbitMqTelemetryHeaders.RetryAttempt] = retryAttempt;
        return sanitized;
    }
}
