using MedicalAssistant.Transcriber.Options;
using MedicalAssistant.Transcriber.Persistence;
using MedicalAssistant.Transcriber.Persistence.Repositories;
using MedicalAssistant.Transcriber.Persistence.Repositories.Interfaces;
using MedicalAssistant.Transcriber.Services;
using MedicalAssistant.Transcriber.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.Configure<BlobStorageOptions>(
            context.Configuration.GetSection(BlobStorageOptions.SectionName));
        services.Configure<AzureSpeechOptions>(
            context.Configuration.GetSection(AzureSpeechOptions.SectionName));
        services.Configure<RabbitMqOptions>(options =>
        {
            options.Connection = context.Configuration["RabbitMqConnection"]
                ?? context.Configuration["RabbitMq:Connection"];
            options.ConsultationTranscriptQueueName =
                context.Configuration["RabbitMqConsultationTranscriptQueueName"]
                ?? context.Configuration["RabbitMq:ConsultationTranscriptQueueName"]
                ?? "consultation.transcript";
        });

        var connectionString = context.Configuration.GetConnectionString("MedicalAssistantDatabasePostgreSQL")
            ?? context.Configuration["ConnectionStrings:MedicalAssistantDatabasePostgreSQL"]
            ?? throw new InvalidOperationException(
                "Connection string 'MedicalAssistantDatabasePostgreSQL' is not configured.");

        services.AddDbContext<TranscriberDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ITranscriptRepository, TranscriptRepository>();
        services.AddScoped<IConsultationRepository, ConsultationRepository>();

        services.AddHttpClient<ISpeechTranscriptionService, AzureSpeechTranscriptionService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
        });

        services.AddSingleton<IConsultationFileRetriever, AzureConsultationFileRetriever>();
        services.AddSingleton<ITranscriptReadyPublisher, RabbitMqTranscriptReadyPublisher>();
        services.AddScoped<IAuditTrailService, AuditTrailService>();
        services.AddScoped<ITranscriptService, TranscriptService>();
    })
    .Build();

host.Run();
