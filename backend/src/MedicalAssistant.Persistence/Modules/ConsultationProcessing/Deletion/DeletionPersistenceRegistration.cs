using MedicalAssistant.Application.Contracts.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Persistence.Modules.ConsultationProcessing.Deletion;

/// <summary>
/// Composition root for Consultation Processing's deletion-cleanup persistence
/// (R38). One call from <see cref="PersistenceServiceRegistration"/> so an
/// owner can see everything Deletion wires here without reading the whole
/// registration file.
/// </summary>
public static class DeletionPersistenceRegistration
{
    public static IServiceCollection AddDeletionPersistence(this IServiceCollection services)
    {
        services.AddScoped<IConsultationDeletionCleanupStore, ConsultationDeletionCleanupStore>();

        return services;
    }
}
