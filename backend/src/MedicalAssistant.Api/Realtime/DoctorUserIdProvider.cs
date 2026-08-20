using Microsoft.AspNetCore.SignalR;

namespace MedicalAssistant.Api.Realtime;

/// <summary>
/// Routes SignalR by doctor identity: the hub user id is the JWT's non-remapped
/// <c>uid</c> claim — the same doctorId every command and audit entry uses — so
/// <c>Clients.User(doctorId)</c> reaches exactly that doctor's connections.
/// </summary>
public sealed class DoctorUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst("uid")?.Value;
}
