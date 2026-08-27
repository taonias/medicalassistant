using MedicalAssistant.Application.Contracts.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Persistence.Modules.CareWorkflow.Consultations;

/// <summary>
/// Composition root for the Consultations module's persistence (R38): the
/// broad <see cref="IConsultationRepository"/> plus the nine narrow capability
/// ports R28 carved out of it (all still backed by the same
/// <see cref="ConsultationRepository"/> — see that class's own doc comment for
/// the strangler-migration rationale), and the retry store. One call from
/// <see cref="PersistenceServiceRegistration"/> so an owner can see everything
/// Consultations wires here without reading the whole registration file.
/// </summary>
public static class ConsultationsPersistenceRegistration
{
    public static IServiceCollection AddConsultationsPersistence(this IServiceCollection services)
    {
        services.AddScoped<IConsultationRepository, ConsultationRepository>();
        services.AddScoped<IConsultationDeletion, ConsultationRepository>();
        services.AddScoped<IConsultationFileRegistration, ConsultationRepository>();
        services.AddScoped<ITranscriptionCompletion, ConsultationRepository>();
        services.AddScoped<IConsultationAccess, ConsultationRepository>();
        services.AddScoped<IConsultationListing, ConsultationRepository>();
        services.AddScoped<IConsultationCreation, ConsultationRepository>();
        services.AddScoped<IConsultationPatientAssignment, ConsultationRepository>();
        services.AddScoped<IConsultationStructuredDataApproval, ConsultationRepository>();
        services.AddScoped<IStructuredDataCompletion, ConsultationRepository>();
        services.AddScoped<IConsultationRetryStore, ConsultationRetryStore>();

        return services;
    }
}
