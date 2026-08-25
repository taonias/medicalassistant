namespace MedicalAssistance.Ingestion.Api.Ingestions;

/// <summary>What came of submitting a Document for ingestion (R26A).</summary>
public enum IngestionSubmissionOutcome
{
    /// <summary>A submission for this document is still Queued or Processing; nothing new was created.</summary>
    AlreadyInFlight,

    /// <summary>This exact content is already ingested (or a failed run of it was just requeued); nothing new was reprocessed.</summary>
    Duplicate,

    /// <summary>A new — or newly requeued — ingestion was accepted.</summary>
    Queued,
}

/// <summary>The outcome of one submission attempt and the Ingestion id it concerns.</summary>
public sealed record IngestionSubmissionResult(IngestionSubmissionOutcome Outcome, Guid IngestionId);

/// <summary>
/// Ingestion acceptance policy (R26A): the dedup/correction decisions and the
/// persist/archive/queue orchestration behind <c>POST /ingestions</c>, extracted
/// from <see cref="MedicalAssistance.Ingestion.Api.Controllers.IngestionsController"/>
/// so the controller stays HTTP validation/mapping only. Submission is a
/// coherent workflow other intake channels can reuse without going through
/// transport code.
/// </summary>
public sealed class IngestionSubmissionService(
    IngestionStore store, IngestionQueue queue, IIngestedDocumentArchive archive)
{
    public async Task<IngestionSubmissionResult> SubmitAsync(IngestionRequest request, CancellationToken ct)
    {
        // Nothing can be decided about a document that has not landed yet, and
        // two workers on one document would race to write its chunk set — so a
        // submission that is still in flight blocks its own resubmission.
        if (await store.FindInFlightAsync(request, ct) is { } inFlightId)
            return new IngestionSubmissionResult(IngestionSubmissionOutcome.AlreadyInFlight, inFlightId);

        // Re-posting content that is already ingested is never new knowledge.
        // After success it is a no-op; after failure it is a retry of the very
        // same ingestion, so the id the caller already holds stays valid and a
        // poison document cannot multiply rows.
        // (While one is still Queued or Processing, T15 turns this into a 409.)
        switch (await store.FindIdenticalAsync(request, ct))
        {
            case { Succeeded: true } completed:
                return new IngestionSubmissionResult(IngestionSubmissionOutcome.Duplicate, completed.Id);

            case { Failed: true } failed:
                // Identical content after a failure is a retry — the same rerun
                // the retry endpoint performs, asked for a different way.
                var (outcome, _) = await store.TryRetryAsync(failed.Id, ct);

                // Unless a correction landed while that one was broken. Sending
                // the original again is then a deliberate return to it, not the
                // recovery of a stale run, so it is ingested as new work below
                // and gets its own ingestion id.
                if (outcome == RetryOutcome.Overtaken)
                    break;

                if (outcome == RetryOutcome.Requeued)
                    await queue.EnqueueAsync(failed.Id, ct);
                return new IngestionSubmissionResult(IngestionSubmissionOutcome.Queued, failed.Id);
        }

        // The same content can also arrive re-filed under a different session or
        // sequence number. It is still the same knowledge about the same
        // patient, so it is skipped too, and the caller is pointed at the
        // ingestion that already holds it.
        if (await store.FindSameContentElsewhereAsync(request, ct) is { } alreadyIngested)
            return new IngestionSubmissionResult(IngestionSubmissionOutcome.Duplicate, alreadyIngested.Id);

        var ingestionId = await store.CreateQueuedAsync(request, ct);

        // Archived before it is handed to a worker, so the landing-zone copy exists
        // before ingestion. Best-effort by contract — it never fails the upload.
        await archive.ArchiveAsync(ingestionId, request, ct);

        await queue.EnqueueAsync(ingestionId, ct);
        return new IngestionSubmissionResult(IngestionSubmissionOutcome.Queued, ingestionId);
    }
}
