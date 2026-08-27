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
        // Order matters: validation runs first (outermost) so invalid requests are never
        // audited; the audit behavior then records successful business commands centrally.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        services.AddScoped<Services.PatientHistoryAssembler>();
        services.AddScoped<Modules.Assistance.Chat.IChatTurnRunner, Modules.Assistance.Chat.ChatTurnRunner>();
        services.AddSingleton<Modules.Assistance.Chat.IConversationSummaryRefreshQueue, Modules.Assistance.Chat.ConversationSummaryRefreshQueue>();
        services.AddScoped<Modules.Assistance.Chat.ConversationSummaryRefresher>();
        services.AddSingleton<Services.IConsultationOutboxRelayObserver, Services.ConsultationOutboxRelayMetrics>();
        services.AddSingleton<Services.IntegrationEventReplayPolicy>();
        services.AddScoped<Services.IntegrationEventReplayService>();
        services.AddSingleton<IValidateOptions<EventRetentionOptions>, EventRetentionOptionsValidator>();
        services.AddSingleton<IValidateOptions<ConsultationOutboxRelayOptions>, ConsultationOutboxRelayOptionsValidator>();

        return services;
    }
}
