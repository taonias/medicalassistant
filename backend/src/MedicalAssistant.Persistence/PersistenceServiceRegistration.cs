using MedicalAssistant.Application.Configuration;
using MedicalAssistant.Application.Contracts.Logging;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Persistence.DatabaseContext;
using MedicalAssistant.Persistence.Logging;
using MedicalAssistant.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Persistence;

public static class PersistenceServiceRegistration
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = RelationalDatabaseProviderParser.FromConfiguration(configuration);
        var connectionString = RelationalDatabaseConnectionStringResolver.Resolve(configuration, provider);

        services.AddDbContext<MedicalAssistantDatabaseContext>(options =>
        {
            switch (provider)
            {
                case RelationalDatabaseProvider.SqlServer:
                    options.UseSqlServer(connectionString, sqlOptions =>
                        sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null));
                    break;
                default:
                    options.UseNpgsql(connectionString, npgsqlOptions =>
                        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null));
                    break;
            }
        });

        services.AddMemoryCache();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IConsultationRepository, ConsultationRepository>();
        services.AddScoped<ITranscriptRepository, TranscriptRepository>();
        services.AddScoped<IMedicalStructuredDataRepository, MedicalStructuredDataRepository>();
        services.AddScoped<IDoctorNoteRepository, DoctorNoteRepository>();
        services.AddScoped<IActionRequestRepository, ActionRequestRepository>();
        services.AddScoped<IConsultationOutboxStore, ConsultationOutboxStore>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IErrorLogger, ErrorLogger>();

        return services;
    }
}
