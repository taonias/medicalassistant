using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MedicalAssistant.Api.Realtime;

/// <summary>
/// The doctor-facing chat progress channel. One-way, server → client: the backend
/// pushes "what the system is doing" phase events to the asking doctor while a turn is
/// in flight. Nothing is invoked on it. Events are a convenience — a client that misses
/// them still gets the whole answer as the ask's HTTP response.
///
/// Routed per doctor via <see cref="DoctorUserIdProvider"/>, so a doctor only ever sees
/// progress for their own questions.
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    /// <summary>The client-side method name the browser subscribes to.</summary>
    public const string ClientMethod = "ChatProgress";
}
