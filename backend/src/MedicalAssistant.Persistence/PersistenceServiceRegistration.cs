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
            // Retry-on-failure (the provider retrying execution strategy) is intentionally
            // NOT enabled on this context. The durable messaging paths use explicit
            // BeginTransactionAsync blocks (inbox/outbox claim, transcription completion,
            // transcript-ready preparation), and a retrying execution strategy rejects
            // user-initiated transactions unless every such block is wrapped in
            // Database.CreateExecutionStrategy().Execute(...). Transient-fault resilience is
            // instead provided at the message layer via RabbitMQ retry queues. Re-enabling
            // DB-level retry requires wrapping those transactions (tracked as a follow-up).
            switch (provider)
            {
                case RelationalDatabaseProvider.SqlServer:
                    options.UseSqlServer(connectionString);
                    break;
                default:
                    options.UseNpgsql(connectionString);
                    break;
            }
        });

        services.AddMemoryCache();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IConsultationRepository, ConsultationRepository>();
        services.AddScoped<ITranscriptRepository, TranscriptRepository>();
        services.AddScoped<ITranscriptionInboxStore, TranscriptionInboxStore>();
        services.AddScoped<ITranscriptionCompletionUnitOfWork, TranscriptionCompletionUnitOfWork>();
        services.AddScoped<ITranscriptReadyPreparationStore, TranscriptReadyPreparationStore>();
        services.AddScoped<IConsultationDeletionCleanupStore, ConsultationDeletionCleanupStore>();
        services.AddScoped<IMedicalStructuredDataRepository, MedicalStructuredDataRepository>();
        services.AddScoped<IDoctorNoteRepository, DoctorNoteRepository>();
        services.AddScoped<IActionRequestRepository, ActionRequestRepository>();
        services.AddScoped<IConsultationOutboxStore, ConsultationOutboxStore>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IErrorLogger, ErrorLogger>();

        return services;
    }
}
