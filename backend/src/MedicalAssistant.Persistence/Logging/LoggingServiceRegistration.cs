using MedicalAssistant.Application.Contracts.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Persistence.Logging;

/// <summary>
/// Composition root for cross-cutting persistence logging (R38): the audit
/// and error loggers every module can depend on. One call from
/// <see cref="PersistenceServiceRegistration"/> so an owner can see everything
/// Logging wires here without reading the whole registration file.
/// </summary>
public static class LoggingServiceRegistration
{
    public static IServiceCollection AddPersistenceLogging(this IServiceCollection services)
    {
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IErrorLogger, ErrorLogger>();

        return services;
    }
}
