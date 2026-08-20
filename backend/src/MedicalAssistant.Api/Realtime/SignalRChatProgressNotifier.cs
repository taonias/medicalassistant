using MedicalAssistant.Application.Features.Chat.Common;
using Microsoft.AspNetCore.SignalR;

namespace MedicalAssistant.Api.Realtime;

/// <summary>
/// Pushes chat progress to the asking doctor over <see cref="ChatHub"/>. Best-effort:
/// a delivery failure is logged and swallowed so a lost progress event can never fail
/// the answer it was describing.
/// </summary>
public sealed class SignalRChatProgressNotifier : IChatProgressNotifier
{
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<SignalRChatProgressNotifier> _logger;

    public SignalRChatProgressNotifier(IHubContext<ChatHub> hub, ILogger<SignalRChatProgressNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotifyAsync(string doctorId, ChatProgressEvent progress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(doctorId))
        {
            return;
        }

        try
        {
            await _hub.Clients.User(doctorId).SendAsync(ChatHub.ClientMethod, progress, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not push {Phase} progress for ask {AskId}; the answer is unaffected",
                progress.Phase,
                progress.AskId);
        }
    }
}
