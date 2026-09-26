using MedicalAssistant.Application.Contracts.Documents;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Contracts.Messaging;
using MedicalAssistant.Application.Contracts.Storage;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Infrastructure.BlobStorage;
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
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        services.Configure<RabbitMqSettings>(configuration.GetSection(RabbitMqSettings.SectionName));

        services.AddScoped(typeof(IAppLogger<>), typeof(LoggerAdapter<>));
        services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
        services.AddScoped<IPdfTextExtractor, PdfPigTextExtractor>();
        services.AddSingleton<ITranscriberProcessingPublisher, RabbitMqTranscriberProcessingPublisher>();
        services.AddSingleton<ITranscriptReadyPublisher, RabbitMqTranscriptReadyPublisher>();
        services.AddSingleton<IAiRequestPublisher, RabbitMqAiRequestPublisher>();
        services.AddHostedService<RabbitMqAiResultConsumer>();

        return services;
    }
}
