namespace MedicalAssistant.Application.Modules.Assistance.Chat;

/// <summary>
/// The ordered "what the system is doing" phases, shared by name across the backend
/// and the AI service (P4.5). The backend emits the first two around its own
/// orchestration; the AI service emits the rest during answer generation. The answer
/// itself is delivered whole over HTTP — never as a phase event.
/// </summary>
public static class ChatPhases
{
    public const string ResolvingPatient = "ResolvingPatient";
    public const string PreparingContext = "PreparingContext";
    public const string RefiningQuestion = "RefiningQuestion";
    public const string SearchingHistory = "SearchingHistory";
    public const string ReviewingEvidence = "ReviewingEvidence";
    public const string ComposingAnswer = "ComposingAnswer";
    public const string VerifyingCitations = "VerifyingCitations";
}

/// <summary>One progress announcement about an in-flight turn, pushed to the asking doctor.</summary>
public sealed record ChatProgressEvent(Guid AskId, string Phase, string Message, DateTimeOffset OccurredAt)
{
    public static ChatProgressEvent Of(Guid askId, string phase, string message) =>
        new(askId, phase, message, DateTimeOffset.UtcNow);
}

/// <summary>
/// Outbound port: pushes a progress event to the asking doctor's live clients. The
/// SignalR implementation lives in the API layer. Delivery is best-effort — a progress
/// event that fails to send is a nuisance, never a reason to fail the answer.
/// </summary>
public interface IChatProgressNotifier
{
    Task NotifyAsync(string doctorId, ChatProgressEvent progress, CancellationToken cancellationToken = default);
}
