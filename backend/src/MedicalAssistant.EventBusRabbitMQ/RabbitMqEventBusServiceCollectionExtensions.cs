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
        services.AddSingleton<IRabbitMqPersistentConnection, RabbitMqPersistentConnection>();
        services.AddSingleton<IRabbitMqDeliveryHandler, RabbitMqIntegrationEventDeliveryHandler>();
        services.AddHostedService<RabbitMqHostedConsumer>();
        return services;
    }
}
