using System.Threading.Channels;
using MedicalAssistant.Application.Contracts.ClinicalKnowledge;
using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.Application.Features.Chat.Common;

/// <summary>
/// In-process hand-off of conversations that may need their rolling summary refreshed.
/// Enqueuing is instant and never blocks the ask's critical path; the work runs on a
/// background service in its own scope.
/// </summary>
public interface IConversationSummaryRefreshQueue
{
    void Enqueue(int conversationId);
    IAsyncEnumerable<int> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class ConversationSummaryRefreshQueue : IConversationSummaryRefreshQueue
{
    private readonly Channel<int> _channel =
        Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(int conversationId) => _channel.Writer.TryWrite(conversationId);

    public IAsyncEnumerable<int> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

/// <summary>
/// The window-aligned decision, isolated so it can be reasoned about and tested on its own:
/// given how far the thread has grown and how far the summary already covers, decide whether
/// a refresh is due and, if so, the sequence to summarize through.
/// </summary>
public static class ConversationSummaryWindow
{
    /// <summary>
    /// The sequence to fold the summary through, or null when no refresh is due — either the
    /// whole thread still fits the verbatim window, or fewer than a window's worth of new
    /// messages have accrued since the last summary. Folding through (max − window) leaves
    /// exactly the last <paramref name="window"/> messages verbatim, with no gap or overlap.
    /// </summary>
    public static int? PlanFoldThrough(int maxSequence, int summarizedThrough, int window)
    {
        if (maxSequence <= window)
        {
            return null;
        }

        var foldThrough = maxSequence - window;
        if (foldThrough - summarizedThrough < window)
        {
            return null;
        }

        return foldThrough;
    }
}

/// <summary>
/// Refreshes a conversation's rolling summary, window-aligned: it does nothing while the
/// whole thread still fits the verbatim window, then folds everything up to
/// (maxSequence − window) once at least a window's worth of new messages have accrued
/// since the last summary. Best-effort — a failure is logged and swallowed; the summary is
/// only ever phrasing/refinement context, never evidence, and retrieval re-runs every turn.
/// </summary>
public sealed class ConversationSummaryRefresher
{
    private const int Window = ChatTurnRunner.RecentWindow;

    private readonly IConversationRepository _conversations;
    private readonly IPatientRepository _patients;
    private readonly IConversationSummarizer _clinicalKnowledge;
    private readonly ILogger<ConversationSummaryRefresher> _logger;

    public ConversationSummaryRefresher(
        IConversationRepository conversations,
        IPatientRepository patients,
        IConversationSummarizer clinicalKnowledge,
        ILogger<ConversationSummaryRefresher> logger)
    {
        _conversations = conversations;
        _patients = patients;
        _clinicalKnowledge = clinicalKnowledge;
        _logger = logger;
    }

    public async Task RefreshAsync(int conversationId, CancellationToken cancellationToken)
    {
        try
        {
            var conversation = await _conversations.GetTrackedByIdAsync(conversationId, cancellationToken);
            if (conversation is null)
            {
                return;
            }

            var maxSequence = await _conversations.GetNextSequenceAsync(conversationId, cancellationToken) - 1;
            var summarizedThrough = conversation.SummarizedThroughSequence ?? 0;

            if (ConversationSummaryWindow.PlanFoldThrough(maxSequence, summarizedThrough, Window)
                is not int foldThrough)
            {
                // Either the thread still fits the verbatim window, or fewer than a window's
                // worth of new messages have accrued since the last summary.
                return;
            }

            var turns = await _conversations.GetMessagesForSummaryAsync(
                conversationId, summarizedThrough, foldThrough, cancellationToken);
            if (turns.Count == 0)
            {
                // Only non-terminal turns in the range; just advance the boundary.
                conversation.UpdateSummary(conversation.RollingSummary ?? string.Empty, foldThrough);
                await _conversations.SaveChangesAsync(cancellationToken);
                return;
            }

            var patient = await _patients.GetByIdAsync(conversation.PatientId);
            var newTurns = turns
                .Select(m => new ClinicalKnowledgeConversationTurn(
                    m.Role == MessageRole.User ? "user" : "assistant", m.Content))
                .ToList();

            var updated = await _clinicalKnowledge.SummarizeConversationAsync(
                new ClinicalKnowledgeSummarizeRequest(
                    ClinicalPatientId.For(patient), conversation.RollingSummary, newTurns),
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(updated))
            {
                conversation.UpdateSummary(updated.Trim(), foldThrough);
                await _conversations.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Conversation summary refresh failed for {ConversationId}", conversationId);
        }
    }
}

/// <summary>Drains the refresh queue, running each refresh in its own scope off the request path.</summary>
public sealed class ConversationSummaryRefreshHostedService : BackgroundService
{
    private readonly IConversationSummaryRefreshQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConversationSummaryRefreshHostedService> _logger;

    public ConversationSummaryRefreshHostedService(
        IConversationSummaryRefreshQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ConversationSummaryRefreshHostedService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var conversationId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var refresher = scope.ServiceProvider.GetRequiredService<ConversationSummaryRefresher>();
                await refresher.RefreshAsync(conversationId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unhandled error refreshing summary for conversation {ConversationId}", conversationId);
            }
        }
    }
}
