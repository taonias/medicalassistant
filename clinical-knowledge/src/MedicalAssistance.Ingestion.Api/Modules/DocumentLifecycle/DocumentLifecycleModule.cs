namespace MedicalAssistance.Ingestion.Api.DocumentLifecycle;

/// <summary>
/// Composition root for the DocumentLifecycle capability (R38): the rolling
/// patient-summary regeneration behind ingestion completion. Split out of
/// <c>IngestionModule</c>, where <see cref="PatientSummaryService"/> — a
/// DocumentLifecycle type — was registered even though it isn't Ingestion's
/// own; this module is DocumentLifecycle's own registration file, matching
/// every sibling capability's one-module-one-registration-file pattern.
/// </summary>
public static class DocumentLifecycleModule
{
    public static IServiceCollection AddDocumentLifecycle(this IServiceCollection services)
    {
        services.AddScoped<PatientSummaryService>();

        return services;
    }
}
