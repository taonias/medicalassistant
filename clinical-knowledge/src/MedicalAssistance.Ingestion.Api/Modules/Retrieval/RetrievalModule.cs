namespace MedicalAssistance.Ingestion.Api.Retrieval;

/// <summary>
/// Composition root for the Retrieval capability (R26): the ordered-step
/// pipeline (ADR-0010/0011). Every stage is registered as an
/// <see cref="IRetrievalStep"/>; the service sorts them by <c>Order</c> and
/// runs them in sequence, so a new stage is one more line here. Internal in
/// v1 — the answer path calls <c>SearchAsync</c> directly, no HTTP surface yet.
/// </summary>
public static class RetrievalModule
{
    public static IServiceCollection AddRetrieval(this IServiceCollection services)
    {
        // The Scope step (Order 10) sets the mandatory patient_id boundary first;
        // embed and search steps join in T41.
        services.AddScoped<IRetrievalStep, ScopeRetrievalStep>();
        services.AddScoped<IRetrievalStep, RefineRetrievalStep>();
        services.AddScoped<IRetrievalStep, EmbedRetrievalStep>();
        services.AddScoped<IRetrievalStep, SearchRetrievalStep>();
        services.AddScoped<IRetrievalStep, PackageRetrievalStep>();
        services.AddScoped<IRetrievalService, RetrievalService>();

        return services;
    }
}
