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
            // Database.CreateExecutionStrategy().Execute(...). Automatic transient-fault retries
            // are not used at the message layer either: each subscriber runs a single queue with
            // a dead-letter safety net, and failures are recorded to the database for doctor-
            // triggered manual retry. Re-enabling DB-level retry requires wrapping those
            // transactions (tracked as a follow-up).
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
        // R28: narrow persistence ports carved out of IConsultationRepository —
        // same EF-backed implementation, each exposed through a use-case-scoped interface.
        services.AddScoped<IConsultationDeletion, ConsultationRepository>();
        services.AddScoped<IConsultationFileRegistration, ConsultationRepository>();
        services.AddScoped<ITranscriptionCompletion, ConsultationRepository>();
        services.AddScoped<IConsultationAccess, ConsultationRepository>();
        services.AddScoped<IConsultationListing, ConsultationRepository>();
        services.AddScoped<IConsultationCreation, ConsultationRepository>();
        services.AddScoped<IConsultationPatientAssignment, ConsultationRepository>();
        services.AddScoped<IConsultationStructuredDataApproval, ConsultationRepository>();
        services.AddScoped<IStructuredDataCompletion, ConsultationRepository>();
        services.AddScoped<ITranscriptRepository, TranscriptRepository>();
        services.AddScoped<ITranscriptionInboxStore, TranscriptionInboxStore>();
        services.AddScoped<ITranscriptionCompletionUnitOfWork, TranscriptionCompletionUnitOfWork>();
        services.AddScoped<ITranscriptReadyPreparationStore, TranscriptReadyPreparationStore>();
        services.AddScoped<IConsultationRetryStore, ConsultationRetryStore>();
        services.AddScoped<IConsultationDeletionCleanupStore, ConsultationDeletionCleanupStore>();
        services.AddScoped<IMedicalStructuredDataRepository, MedicalStructuredDataRepository>();
        services.AddScoped<IDoctorNoteRepository, DoctorNoteRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IActionRequestRepository, ActionRequestRepository>();
        services.AddScoped<IConsultationOutboxStore, ConsultationOutboxStore>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IErrorLogger, ErrorLogger>();

        return services;
    }
}
