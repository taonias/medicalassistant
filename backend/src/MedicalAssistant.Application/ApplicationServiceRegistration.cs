using FluentValidation;
using MedicalAssistant.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using MedicalAssistant.Application.Models;

namespace MedicalAssistant.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddAutoMapper(Assembly.GetExecutingAssembly());
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddMediatR(Assembly.GetExecutingAssembly());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<Services.PatientHistoryAssembler>();
        services.AddSingleton<Services.IConsultationOutboxRelayObserver, Services.ConsultationOutboxRelayMetrics>();
        services.AddSingleton<Services.IntegrationEventReplayPolicy>();
        services.AddSingleton<IValidateOptions<EventRetentionOptions>, EventRetentionOptionsValidator>();

        return services;
    }
}
