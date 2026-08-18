using MedicalAssistant.Application;
using MedicalAssistant.Persistence;
using MedicalAssistant.Transcription.Worker;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddApplicationServices();
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddTranscriptionWorkerServices(builder.Configuration);

// OpenTelemetry traces + metrics + logs, exported over OTLP only when
// OTEL_EXPORTER_OTLP_ENDPOINT is configured (e.g. the central Aspire dashboard).
const string ServiceName = "MedicalAssistant.Transcription.Worker";
var otlpConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(ServiceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddHttpClientInstrumentation()
            // Database spans from Npgsql's built-in ActivitySource (no extra package).
            .AddSource("Npgsql");
        if (otlpConfigured)
            tracing.AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddHttpClientInstrumentation()
            .AddMeter("MedicalAssistant.EventBusRabbitMQ");
        if (otlpConfigured)
            metrics.AddOtlpExporter();
    });

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeScopes = true;
    logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ServiceName));
    if (otlpConfigured)
        logging.AddOtlpExporter();
});

var host = builder.Build();
await host.RunAsync();

public partial class Program;
