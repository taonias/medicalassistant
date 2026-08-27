using MedicalAssistant.Application.Contracts.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Persistence.Modules.CareWorkflow.StructuredMedicalData;

/// <summary>
/// Composition root for the Structured Medical Data module's persistence (R38).
/// One call from <see cref="PersistenceServiceRegistration"/> so an owner can
/// see everything Structured Medical Data wires here without reading the whole
/// registration file.
/// </summary>
public static class StructuredMedicalDataPersistenceRegistration
{
    public static IServiceCollection AddStructuredMedicalDataPersistence(this IServiceCollection services)
    {
        services.AddScoped<IMedicalStructuredDataRepository, MedicalStructuredDataRepository>();

        return services;
    }
}
