using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.EventBusRabbitMQ;

public static class RabbitMqEventBusServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqEventBusConsumer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqConnectionOptions>(
            configuration.GetSection(RabbitMqConnectionOptions.SectionName));
        services.AddSingleton<IValidateOptions<RabbitMqConnectionOptions>, RabbitMqConnectionOptionsValidator>();
        services.Configure<RabbitMqConsumerOptions>(
            configuration.GetSection(RabbitMqConsumerOptions.SectionName));
        services.AddSingleton<IValidateOptions<RabbitMqConsumerOptions>, RabbitMqConsumerOptionsValidator>();
        services.Configure<RabbitMqTopologyOptions>(
            configuration.GetSection(RabbitMqTopologyOptions.SectionName));
        services.Configure<RabbitMqPublishOptions>(
            configuration.GetSection(RabbitMqPublishOptions.SectionName));
        services.AddSingleton<IRabbitMqPersistentConnection, RabbitMqPersistentConnection>();
        services.AddSingleton<IRabbitMqDeliveryHandler, RabbitMqIntegrationEventDeliveryHandler>();
        services.AddSingleton<IRabbitMqDeliveryObserver, RabbitMqDeliveryMetrics>();
        services.AddSingleton<RabbitMqConfirmedPublisher>();
        services.AddSingleton<RabbitMqSubscriberTopologyDeclarer>();
        services.AddHostedService<RabbitMqHostedConsumer>();
        services.AddHealthChecks()
            .AddCheck<RabbitMqSubscriberReadinessHealthCheck>(
                "rabbitmq_subscriber_readiness",
                tags: ["ready"]);
        return services;
    }
}
