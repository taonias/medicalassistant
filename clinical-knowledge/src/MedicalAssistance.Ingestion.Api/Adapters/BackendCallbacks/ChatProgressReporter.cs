using System.Net.Http.Json;

namespace MedicalAssistance.Ingestion.Api.GroundedChat;

/// <summary>
/// The AI-emitted "what the system is doing" phases (P4.5), mirroring the backend's
/// ChatPhases by name. The AI emits the retrieval/generation phases; the backend emits
/// ResolvingPatient/PreparingContext.
/// </summary>
public static class ChatPhases
{
    public const string SearchingHistory = "SearchingHistory";
    public const string ComposingAnswer = "ComposingAnswer";
    public const string VerifyingCitations = "VerifyingCitations";
}

/// <summary>Config for calling the backend's chat-progress callback.</summary>
public sealed class BackendCallbackOptions
{
    public const string SectionName = "BackendCallback";

    /// <summary>Base URL of the backend API (e.g. http://backend-api:8080).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>The shared X-Api-Key the backend's ai-callback endpoints require.</summary>
    public string? ApiKey { get; set; }
}

/// <summary>
/// Reports a real phase boundary to the backend, which relays it to the asking doctor
/// over SignalR. Best-effort and fire-and-forget: a failed or unconfigured callback is
/// swallowed — losing a progress line is a nuisance, never a reason to fail the answer.
/// </summary>
public interface IChatProgressReporter
{
    Task ReportAsync(Guid? askId, string? doctorId, string phase, string message, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class ChatProgressReporter(
    HttpClient httpClient, ILogger<ChatProgressReporter> logger) : IChatProgressReporter
{
    public async Task ReportAsync(
        Guid? askId, string? doctorId, string phase, string message, CancellationToken cancellationToken)
    {
        // Nothing to correlate to a client — skip silently.
        if (askId is null || askId == Guid.Empty || string.IsNullOrWhiteSpace(doctorId))
        {
            return;
        }

        try
        {
            await httpClient.PostAsJsonAsync(
                "api/ai-callback/chat-progress",
                new
                {
                    askId,
                    doctorId,
                    phase,
                    message,
                    occurredAt = DateTimeOffset.UtcNow,
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Chat progress callback for {Phase} could not be delivered", phase);
        }
    }
}
