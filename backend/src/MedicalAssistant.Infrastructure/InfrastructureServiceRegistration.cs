using MedicalAssistant.Application.Contracts.AiModule;
using MedicalAssistant.Application.Contracts.ClinicalKnowledge;
using MedicalAssistant.Application.Contracts.Documents;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Contracts.Storage;
using MedicalAssistant.Application.EventHandlers;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Services;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;
using MedicalAssistant.Infrastructure.AiModule;
using MedicalAssistant.Infrastructure.BlobStorage;
using MedicalAssistant.Infrastructure.ClinicalKnowledge;
using MedicalAssistant.Infrastructure.Documents;
using MedicalAssistant.Infrastructure.Logging;
using MedicalAssistant.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BlobStorageSettings>(configuration.GetSection("BlobStorage"));
        services.Configure<AiModuleSettings>(configuration.GetSection("AiModule"));
        services.Configure<ClinicalKnowledgeSettings>(configuration.GetSection(ClinicalKnowledgeSettings.SectionName));
        services.Configure<AiCallbackSettings>(configuration.GetSection("AiCallback"));
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        services.Configure<ConsultationOutboxRelayOptions>(configuration.GetSection(ConsultationOutboxRelayOptions.SectionName));
        services.Configure<RabbitMqSettings>(configuration.GetSection(RabbitMqSettings.SectionName));
        services.Configure<RabbitMqConnectionOptions>(configuration.GetSection(RabbitMqConnectionOptions.SectionName));
        services.Configure<RabbitMqPublishOptions>(configuration.GetSection(RabbitMqPublishOptions.SectionName));

        services.AddScoped(typeof(IAppLogger<>), typeof(LoggerAdapter<>));
        services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
        services.AddScoped<IConsultationBlobCleanupService, ConsultationBlobCleanupService>();
        services.AddScoped<IPdfTextExtractor, PdfPigTextExtractor>();
        services.AddHttpClient<IAiModuleClient, AiModuleHttpClient>();
        services.AddHttpClient<IClinicalKnowledgeClient, ClinicalKnowledgeHttpClient>();
        services.AddScoped<ConsultationOutboxRelay>();
        services.AddHostedService<ConsultationOutboxRelayHostedService>();
        services.AddSingleton<IRabbitMqPersistentConnection, RabbitMqPersistentConnection>();
        services.AddSingleton<RabbitMqConfirmedPublisher>();
        services.AddSingleton<IConsultationOutboxPublisher, RabbitMqConsultationOutboxPublisher>();
        services.AddSingleton<IConsultationProcessingPublisher, RabbitMqConsultationProcessingPublisher>();
        services.AddSingleton<ITranscriptReadyPublisher, RabbitMqTranscriptReadyPublisher>();
        services.AddScoped<ConsultationTranscriptReadyIntegrationEventHandler>();
        services.AddScoped<ConsultationDeletedIntegrationEventHandler>();
        services.AddSingleton(ConsultationIntegrationEvents.Registry);
        services.AddSingleton(provider =>
            IntegrationEventSubscriptionRegistry.Create(
                provider.GetRequiredService<IntegrationEventContractRegistry>(),
                subscriptions => subscriptions.Subscribe<
                    ConsultationTranscriptReadyV1,
                    ConsultationTranscriptReadyIntegrationEventHandler>()
                    .Subscribe<
                    ConsultationDeletedV1,
                    ConsultationDeletedIntegrationEventHandler>()));
        services.AddSingleton<IntegrationEventDispatcher>();
        services.AddRabbitMqEventBusConsumer(configuration);
        services.PostConfigure<RabbitMqTopologyOptions>(options =>
        {
            options.ExchangeName = string.IsNullOrWhiteSpace(options.ExchangeName)
                ? "medicalassistant.events"
                : options.ExchangeName;
            options.SubscriberName = string.IsNullOrWhiteSpace(options.SubscriberName)
                ? ConsultationTranscriptReadyIntegrationEventHandler.ConsumerName
                : options.SubscriberName;
            options.QueueName = string.IsNullOrWhiteSpace(options.QueueName)
                ? "medicalassistant.backend.transcript-ready"
                : options.QueueName;
        });
        services.PostConfigure<RabbitMqConsumerOptions>(options =>
        {
            options.QueueName = string.IsNullOrWhiteSpace(options.QueueName)
                ? "medicalassistant.backend.transcript-ready"
                : options.QueueName;
        });

        return services;
    }
}
