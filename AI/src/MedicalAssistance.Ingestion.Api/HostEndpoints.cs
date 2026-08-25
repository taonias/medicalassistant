using MedicalAssistance.Ingestion.Api.Realtime;

namespace MedicalAssistance.Ingestion.Api;

/// <summary>
/// Endpoint mapping for the host (R26): Swagger UI, the authentication/authorization
/// middleware, controller routes, and the ingestion-status SignalR hub. One method
/// rather than several — <c>MapControllers()</c> auto-discovers every module's
/// controllers by reflection, so there is no natural per-module split here.
/// </summary>
public static class HostEndpoints
{
    public static WebApplication MapIngestionApiEndpoints(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Clinical Document Ingestion API v1");
        });

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        // The hub carries no authorization metadata of its own, so the fallback policy
        // applies: the handshake needs the same secret every other endpoint needs.
        app.MapHub<IngestionStatusHub>("/hubs/ingestion-status");

        return app;
    }
}
