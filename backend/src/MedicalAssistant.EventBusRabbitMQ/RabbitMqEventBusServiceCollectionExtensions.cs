using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.EventBusRabbitMQ;

public static class RabbitMqEventBusServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqEventBusConsumer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqConnectionOptions>(
            configuration.GetSection(RabbitMqConnectionOptions.SectionName));
        services.Configure<RabbitMqConsumerOptions>(
            configuration.GetSection(RabbitMqConsumerOptions.SectionName));
        services.Configure<RabbitMqTopologyOptions>(
            configuration.GetSection(RabbitMqTopologyOptions.SectionName));
        services.Configure<RabbitMqPublishOptions>(
            configuration.GetSection(RabbitMqPublishOptions.SectionName));
        services.AddSingleton<IRabbitMqPersistentConnection, RabbitMqPersistentConnection>();
        services.AddSingleton<IRabbitMqDeliveryHandler, RabbitMqIntegrationEventDeliveryHandler>();
        services.AddSingleton<RabbitMqConfirmedPublisher>();
        services.AddSingleton<RabbitMqSubscriberTopologyDeclarer>();
        services.AddHostedService<RabbitMqHostedConsumer>();
        return services;
    }
}
