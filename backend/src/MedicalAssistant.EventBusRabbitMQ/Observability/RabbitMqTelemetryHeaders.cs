namespace MedicalAssistant.EventBusRabbitMQ;

public static class RabbitMqTelemetryHeaders
{
    public const string RetryAttempt = "x-medicalassistant-retry-attempt";
    public const string TraceParent = "traceparent";
    public const string TraceState = "tracestate";
}
