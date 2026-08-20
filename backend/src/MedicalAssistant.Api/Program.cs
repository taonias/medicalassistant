using MedicalAssistant.Api.Middleware;
using MedicalAssistant.Application;
using MedicalAssistant.Identity;
using MedicalAssistant.Infrastructure;
using MedicalAssistant.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
var otlpConfigured = !string.IsNullOrWhiteSpace(otlpEndpoint);

builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig
        .WriteTo.Console()
        .ReadFrom.Configuration(context.Configuration);

    // Ship logs to the central OTLP collector (Aspire dashboard) when configured.
    if (otlpConfigured)
    {
        loggerConfig.WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = otlpEndpoint;
            options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = "MedicalAssistant.Api"
            };
        });
    }
});

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddIdentityServices(builder.Configuration);

// OpenTelemetry traces + metrics (logs are exported via the Serilog OTLP sink
// above), sent over OTLP only when OTEL_EXPORTER_OTLP_ENDPOINT is configured.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("MedicalAssistant.Api"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            // Database spans from Npgsql's built-in ActivitySource (no extra package).
            .AddSource("Npgsql");
        if (otlpConfigured)
            tracing.AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("MedicalAssistant.ConsultationOutbox")
            .AddMeter("MedicalAssistant.EventBusRabbitMQ");
        if (otlpConfigured)
            metrics.AddOtlpExporter();
    });

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// Real-time chat progress: the browser connects to /hubs/chat and receives phase events
// routed by doctorId while a turn is in flight (the answer itself is not streamed).
builder.Services.AddSignalR();
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, MedicalAssistant.Api.Realtime.DoctorUserIdProvider>();
builder.Services.AddScoped<MedicalAssistant.Application.Features.Chat.Common.IChatProgressNotifier, MedicalAssistant.Api.Realtime.SignalRChatProgressNotifier>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("all", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:4200", "http://localhost:5500"];
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Medical Assistant API",
        Version = "v1"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Token in the format 'Bearer {token}'"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors("all");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapControllers();
app.MapHub<MedicalAssistant.Api.Realtime.ChatHub>("/hubs/chat");

await IdentityDbInitializer.SeedRolesAsync(app.Services);
await IdentityDbInitializer.SeedDevelopmentDoctorAsync(app.Services);

app.Run();

public partial class Program { }
