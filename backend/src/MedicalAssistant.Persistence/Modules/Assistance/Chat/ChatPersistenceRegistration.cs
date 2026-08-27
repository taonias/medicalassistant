using MedicalAssistant.Application.Contracts.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MedicalAssistant.Persistence.Modules.Assistance.Chat;

/// <summary>
/// Composition root for the Chat module's persistence (R38). One call from
/// <see cref="PersistenceServiceRegistration"/> so an owner can see everything
/// Chat wires here without reading the whole registration file.
/// </summary>
public static class ChatPersistenceRegistration
{
    public static IServiceCollection AddChatPersistence(this IServiceCollection services)
    {
        services.AddScoped<IConversationRepository, ConversationRepository>();

        return services;
    }
}
