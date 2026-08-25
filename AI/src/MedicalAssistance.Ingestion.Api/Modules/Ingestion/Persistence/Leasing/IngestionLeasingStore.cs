using Microsoft.EntityFrameworkCore;

namespace MedicalAssistance.Ingestion.Api.Ingestions;

/// <summary>What came of asking for an Ingestion to be rerun.</summary>
public enum RetryOutcome
{
    /// <summary>No Ingestion has that id.</summary>
    NotFound,

    /// <summary>It had failed, and is now queued for a complete rerun.</summary>
    Requeued,

    /// <summary>It is not in a state that can be rerun — only a Failed Ingestion can.</summary>
    NotRetryable,

    /// <summary>
    /// It failed, but a later submission of the same Document has completed since.
    /// Rerunning it would replace the newer version with the older one.
    /// </summary>
    Overtaken,
}

/// <summary>What came of a worker trying to take an Ingestion off the queue.</summary>
public enum ClaimOutcome
{
    /// <summary>The attempt is counted and the Ingestion is now Processing.</summary>
    Claimed,

    /// <summary>
    /// It is no longer unfinished, so there is nothing here to run: some other
    /// run carried it to a terminal state while this queue entry waited its turn.
    /// </summary>
    NotClaimable,

    /// <summary>
    /// It is still unfinished but has used up its attempts, and must be failed
    /// rather than started again.
    /// </summary>
    AttemptsExhausted,
}

/// <summary>
/// Who owns doing the work and when to reclaim it: claiming an Ingestion for
/// a worker, retrying a failed one, and finding what a crash or deploy left
/// unfinished.
/// </summary>
internal sealed class IngestionLeasingStore(IngestionDbContext db)
{
    /// <summary>
    /// Returns a Failed Ingestion to the queue for a fresh, complete rerun from
    /// its stored payload — there are no stage checkpoints to resume from
    /// (ADR-0003), so the earlier failure leaves nothing to clean up. Only a
    /// Failed ingestion can be rerun; the outcome says why not, when not.
    ///
    /// The attempt count starts over. Deciding to rerun is a deliberate act,
    /// usually taken because whatever broke has been fixed; inheriting a spent
    /// budget would leave a document unrecoverable forever because of an outage
    /// that is long over.
    ///
    /// A failure that a later submission has already completed over is
    /// <see cref="RetryOutcome.Overtaken"/> rather than rerunnable. Completing it
    /// would supersede the newer version with the older one, silently reverting
    /// the document to text a doctor had already replaced — the one outcome a
    /// rerun must never produce.
    /// </summary>
    public async Task<(RetryOutcome Outcome, string? CurrentStatus)> TryRetryAsync(
        Guid id, CancellationToken ct = default)
    {
        // Both conditions live in the same statement as the update, so neither a
        // worker picking this up nor a correction landing mid-call can slip
        // between the check and the requeue.
        var requeued = await db.Ingestions
            .Where(i => i.Id == id
                        && i.Status == "Failed"
                        && !db.Ingestions.Any(newer =>
                            newer.Id != i.Id
                            && newer.Status == "Completed"
                            && newer.CreatedAt > i.CreatedAt
                            && newer.DocumentId == i.DocumentId))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(i => i.Status, "Queued")
                    .SetProperty(i => i.ErrorMessage, (string?)null)
                    .SetProperty(i => i.Attempts, 0)
                    .SetProperty(i => i.UpdatedAt, DateTimeOffset.UtcNow),
                ct);

        if (requeued > 0)
            return (RetryOutcome.Requeued, "Failed");

        var current = await db.Ingestions.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => i.Status)
            .FirstOrDefaultAsync(ct);

        // Still Failed, yet not requeued: the only other condition is the one
        // above, so a newer version of this document has landed.
        return current switch
        {
            null => (RetryOutcome.NotFound, null),
            "Failed" => (RetryOutcome.Overtaken, current),
            _ => (RetryOutcome.NotRetryable, current),
        };
    }

    /// <summary>
    /// Claims an Ingestion for a worker: counts the attempt and moves it to
    /// Processing, in one statement so two workers cannot both claim it.
    ///
    /// Only an unfinished Ingestion can be claimed — the same Queued-or-Processing
    /// condition <see cref="FindUnfinishedAsync"/> selects on, because what is
    /// recoverable and what is runnable are the same thing. A queue entry can
    /// outlive the run it named: recovery hands one id to more than one instance
    /// by design, and the advisory lock only stops them running it at the same
    /// time, not one of them arriving after the other has finished. Without the
    /// status in the condition, that late arrival re-runs a Completed Ingestion
    /// and its commit supersedes whatever correction landed in the meantime.
    ///
    /// The two refusals are kept apart deliberately: attempts spent is a
    /// document that has to be failed, while no longer claimable is work that
    /// is simply not this worker's any more. Reporting the second as the first
    /// would mark a finished ingestion Failed.
    /// </summary>
    public async Task<ClaimOutcome> TryClaimAsync(Guid id, int maxAttempts, CancellationToken ct = default)
    {
        var claimed = await db.Ingestions
            .Where(i => i.Id == id
                        && (i.Status == "Queued" || i.Status == "Processing")
                        && i.Attempts < maxAttempts)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(i => i.Status, "Processing")
                    .SetProperty(i => i.Attempts, i => i.Attempts + 1)
                    .SetProperty(i => i.UpdatedAt, DateTimeOffset.UtcNow),
                ct);

        if (claimed > 0)
            return ClaimOutcome.Claimed;

        // Not claimed, so exactly one of the two conditions failed. Reading the
        // status back says which — and an id with no row at all is nobody's work.
        var status = await db.Ingestions.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => i.Status)
            .FirstOrDefaultAsync(ct);

        return status is "Queued" or "Processing"
            ? ClaimOutcome.AttemptsExhausted
            : ClaimOutcome.NotClaimable;
    }

    /// <summary>
    /// Ids of every Ingestion that was accepted but never reached a terminal
    /// state — what a crash or a deploy leaves behind. Swept up and queued
    /// again, because an accepted upload is a promise, and a doctor watching a
    /// progress bar has no way to know the process died.
    ///
    /// This is every unfinished Ingestion, including the ones another instance
    /// is running right now. Which of them are actually abandoned is a question
    /// about who holds their advisory locks, and it is
    /// <see cref="IngestionRecoverySweep" /> that asks it.
    /// </summary>
    public Task<List<Guid>> FindUnfinishedAsync(CancellationToken ct = default) =>
        db.Ingestions.AsNoTracking()
            .Where(i => i.Status == "Queued" || i.Status == "Processing")
            .OrderBy(i => i.CreatedAt)
            .Select(i => i.Id)
            .ToListAsync(ct);

    // NOTE (R25 finding, not fixed here): no call site anywhere in the codebase
    // invokes this method — it predates the persistence split and appears to be
    // dead code. Carried forward unchanged rather than silently dropped; flagged
    // for the team to decide whether to remove it.
    private async Task UpdateStatusAsync(Guid id, string status, string? errorMessage, CancellationToken ct)
    {
        await db.Ingestions.Where(i => i.Id == id).ExecuteUpdateAsync(setters => setters
            .SetProperty(i => i.Status, status)
            .SetProperty(i => i.ErrorMessage, errorMessage)
            .SetProperty(i => i.UpdatedAt, DateTimeOffset.UtcNow), ct);
    }
}
