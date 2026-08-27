using MedicalAssistant.Application.Configuration;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Persistence.DatabaseContext;
using MedicalAssistant.Persistence.Logging;
using MedicalAssistant.Persistence.Modules.Assistance.Chat;
using MedicalAssistant.Persistence.Modules.CareWorkflow.Consultations;
using MedicalAssistant.Persistence.Modules.CareWorkflow.DoctorNotes;
using MedicalAssistant.Persistence.Modules.CareWorkflow.Patients;
using MedicalAssistant.Persistence.Modules.CareWorkflow.StructuredMedicalData;
using MedicalAssistant.Persistence.Modules.CareWorkflow.Transcripts;
using MedicalAssistant.Persistence.Modules.ConsultationProcessing.Deletion;
using MedicalAssistant.Persistence.Modules.ConsultationProcessing.DurableMessaging;
using MedicalAssistant.Persistence.Modules.ConsultationProcessing.TranscriptIngestion;
using MedicalAssistant.Persistence.Modules.Integrations.LegacyAiModule;
using MedicalAssistant.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Persistence;

/// <summary>
/// Composition root for the Persistence layer (R38): the DbContext, the
/// generic repository (the one truly cross-module registration — every
/// other capability's own store lives in its own module), and one call per
/// module's own <c>Add&lt;Name&gt;Persistence()</c> — each registered in that
/// module's own folder, alongside the store classes it wires, the same
/// pattern the AI service's own <c>IngestionModule</c> established (R26). An
/// owner adding or changing what one
/// module registers touches exactly that module's own registration file,
/// never this one.
/// </summary>
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
        // The one truly cross-module registration: every module's own store still
        // gets its own AddScoped<TInterface, TImplementation> line in its own
        // registration file below, but the open-generic repository itself belongs
        // to no single module.
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        services.AddPatientsPersistence();
        services.AddConsultationsPersistence();
        services.AddTranscriptsPersistence();
        services.AddStructuredMedicalDataPersistence();
        services.AddDoctorNotesPersistence();
        services.AddChatPersistence();
        services.AddLegacyAiModulePersistence();
        services.AddDurableMessagingPersistence();
        services.AddTranscriptIngestionPersistence();
        services.AddDeletionPersistence();
        services.AddPersistenceLogging();

        return services;
    }
}
