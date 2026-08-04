using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;
using MedicalAssistant.Transcription.Worker.Handlers;
using MedicalAssistant.Transcription.Worker.Health;
using MedicalAssistant.Transcription.Worker.Options;
using MedicalAssistant.Transcription.Worker.Speech;
using MedicalAssistant.Transcription.Worker.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Transcription.Worker;

public static class TranscriptionWorkerServiceRegistration
{
    public static IServiceCollection AddTranscriptionWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TranscriptionWorkerOptions>(
            configuration.GetSection(TranscriptionWorkerOptions.SectionName));
        services.Configure<TranscriptionBlobRetrievalOptions>(
            configuration.GetSection(TranscriptionBlobRetrievalOptions.SectionName));
        services.Configure<AzureSpeechTranscriptionOptions>(
            configuration.GetSection(AzureSpeechTranscriptionOptions.SectionName));

        services.AddScoped<IPrivateBlobObjectClient, AzurePrivateBlobObjectClient>();
        services.AddScoped<IConsultationAudioBlobRetriever, ConsultationAudioBlobRetriever>();
        services.AddHttpClient<ISpeechTranscriptionService, AzureSpeechTranscriptionService>();
        services.AddScoped<ConsultationAudioUploadedIntegrationEventHandler>();
        services.AddSingleton(ConsultationIntegrationEvents.Registry);
        services.AddSingleton(provider =>
            IntegrationEventSubscriptionRegistry.Create(
                provider.GetRequiredService<IntegrationEventContractRegistry>(),
                subscriptions => subscriptions.Subscribe<
                    ConsultationAudioUploadedV1,
                    ConsultationAudioUploadedIntegrationEventHandler>()));
        services.AddSingleton<IntegrationEventDispatcher>();
        services.AddRabbitMqEventBusConsumer(configuration);
        services.AddHealthChecks()
            .AddCheck<TranscriptionWorkerReadinessHealthCheck>("transcription_worker_readiness");

        return services;
    }
}
