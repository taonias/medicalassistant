using MedicalAssistant.Application.Contracts.AiModule;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Contracts.Storage;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Infrastructure.AiModule;
using MedicalAssistant.Infrastructure.BlobStorage;
using MedicalAssistant.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BlobStorageSettings>(configuration.GetSection("BlobStorage"));
        services.Configure<AiModuleSettings>(configuration.GetSection("AiModule"));
        services.Configure<AiCallbackSettings>(configuration.GetSection("AiCallback"));
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));

        services.AddScoped(typeof(IAppLogger<>), typeof(LoggerAdapter<>));
        services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
        services.AddHttpClient<IAiModuleClient, AiModuleHttpClient>();

        return services;
    }
}
