using MedicalAssistant.Application.Contracts.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Persistence.Modules.Integrations.LegacyAiModule;

/// <summary>
/// Composition root for the Legacy AI Module integration's persistence (R38).
/// One call from <see cref="PersistenceServiceRegistration"/> so an owner can
/// see everything this integration wires here without reading the whole
/// registration file.
/// </summary>
public static class LegacyAiModulePersistenceRegistration
{
    public static IServiceCollection AddLegacyAiModulePersistence(this IServiceCollection services)
    {
        services.AddScoped<IActionRequestRepository, ActionRequestRepository>();

        return services;
    }
}
