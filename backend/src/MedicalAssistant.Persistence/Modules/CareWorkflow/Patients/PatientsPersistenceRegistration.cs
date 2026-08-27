using MedicalAssistant.Application.Contracts.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Persistence.Modules.CareWorkflow.Patients;

/// <summary>
/// Composition root for the Patients module's persistence (R38): one call from
/// <see cref="PersistenceServiceRegistration"/> so an owner can see everything
/// Patients wires here without reading the whole registration file.
/// </summary>
public static class PatientsPersistenceRegistration
{
    public static IServiceCollection AddPatientsPersistence(this IServiceCollection services)
    {
        services.AddScoped<IPatientRepository, PatientRepository>();

        return services;
    }
}
