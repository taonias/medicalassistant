using MedicalAssistant.Application.Contracts.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Persistence.Modules.CareWorkflow.DoctorNotes;

/// <summary>
/// Composition root for the Doctor Notes module's persistence (R38). One call
/// from <see cref="PersistenceServiceRegistration"/> so an owner can see
/// everything Doctor Notes wires here without reading the whole registration file.
/// </summary>
public static class DoctorNotesPersistenceRegistration
{
    public static IServiceCollection AddDoctorNotesPersistence(this IServiceCollection services)
    {
        services.AddScoped<IDoctorNoteRepository, DoctorNoteRepository>();

        return services;
    }
}
