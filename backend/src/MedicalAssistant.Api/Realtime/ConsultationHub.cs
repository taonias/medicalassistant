using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MedicalAssistant.Api.Realtime;

/// <summary>
/// The doctor-facing consultation status channel. One-way, server → client: the
/// backend pushes "a consultation you're watching just changed" pings so the detail
/// page can refetch instead of polling. Nothing is invoked on it. A missed event is a
/// convenience loss, not a correctness issue — the doctor's next manual refresh (or a
/// reconnect) always reflects the real, durable state.
///
/// Routed per doctor via <see cref="DoctorUserIdProvider"/>, so a doctor only ever
/// sees updates for their own consultations.
/// </summary>
[Authorize]
public sealed class ConsultationHub : Hub
{
    /// <summary>The client-side method name the browser subscribes to.</summary>
    public const string ClientMethod = "ConsultationStatusChanged";
}
