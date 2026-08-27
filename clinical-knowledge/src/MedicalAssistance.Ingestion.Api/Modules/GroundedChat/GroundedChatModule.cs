namespace MedicalAssistance.Ingestion.Api.GroundedChat;

/// <summary>
/// Composition root for the GroundedChat capability (R26): the stateless
/// answer orchestration behind POST /patients/{id}/chat/answer (ADR-0010/0012)
/// — retrieve, generate over the evidence, package citations — plus the
/// backend chat-progress callback, which is namespace-owned by chat even
/// though it physically lives under <c>Adapters/BackendCallbacks</c>.
/// </summary>
public static class GroundedChatModule
{
    public static IServiceCollection AddGroundedChat(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IGroundedAnswerGenerator, GroundedAnswerGenerator>();
        services.AddScoped<IGroundedAnswerService, GroundedAnswerService>();
        services.AddScoped<IConversationSummarizer, ConversationSummarizer>();

        // AI → backend chat-progress callback: emits real phase boundaries mid-answer, which the
        // backend relays to the asking doctor over SignalR. Best-effort; short timeout.
        services.Configure<BackendCallbackOptions>(configuration.GetSection(BackendCallbackOptions.SectionName));
        services.AddHttpClient<IChatProgressReporter, ChatProgressReporter>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BackendCallbackOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
                client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        return services;
    }
}
