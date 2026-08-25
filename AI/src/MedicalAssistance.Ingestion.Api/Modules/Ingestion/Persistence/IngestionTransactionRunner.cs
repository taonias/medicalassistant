using System.Data;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistance.Ingestion.Api.Ingestions;

/// <summary>
/// The one place a write becomes atomic. Every operation that changes more
/// than one row — the supersede-and-insert commit, un-ingest, erasure — runs
/// its body here, so beginning, committing, rolling back, and choosing the
/// isolation level happen in a single spot instead of being re-established,
/// and left to diverge, in each method. The body either returns and the whole
/// of it commits, or it throws and disposing the transaction rolls all of it
/// back: nothing a body wrote is ever left half-applied, and a body that
/// returns early having written nothing simply commits an empty transaction.
///
/// Read Committed is set explicitly rather than inherited from the connection
/// default, so the isolation the whole service's writes run under is stated in
/// one readable place. Strengthening it — to close a write-skew such as B18 —
/// is a change to this line, and would want an execution strategy wrapped
/// around the body to retry the serialization failures a stricter level
/// introduces. (The provider is on its non-retrying default today, which is
/// why the body is not required to be idempotent.)
/// </summary>
internal static class IngestionTransactionRunner
{
    public static async Task<T> InTransactionAsync<T>(
        this IngestionDbContext db, Func<CancellationToken, Task<T>> body, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var result = await body(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    /// <inheritdoc cref="InTransactionAsync{T}" />
    public static Task InTransactionAsync(
        this IngestionDbContext db, Func<CancellationToken, Task> body, CancellationToken ct) =>
        db.InTransactionAsync<object?>(async innerCt => { await body(innerCt); return null; }, ct);
}
