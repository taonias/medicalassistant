using Microsoft.EntityFrameworkCore;

namespace MedicalAssistance.Ingestion.Api.Ingestions;

/// <summary>What came of asking to un-ingest a Document.</summary>
public enum UnIngestOutcome
{
    /// <summary>The live version's chunks and payload were removed and its Ingestion is now a Deleted tombstone.</summary>
    Deleted,

    /// <summary>No live version of this Document exists to remove — the id is unknown, or it was already un-ingested.</summary>
    NotFound,

    /// <summary>A version of this Document is still Queued or Processing; it cannot be removed until that run settles.</summary>
    InFlight,
}

/// <summary>
/// Deliberate removal of clinical data: a single document (un-ingest) or a
/// whole patient's record (GDPR erasure), each in one transaction so the
/// removal and its audit trail can never come apart.
/// </summary>
internal sealed class IngestionCleanupStore(IngestionDbContext db)
{
    /// <summary>
    /// GDPR Erasure: removes everything the service holds about a patient — all
    /// chunks and every Ingestion row, the Deleted tombstones un-ingest leaves
    /// included — and writes one <see cref="ErasureLogEntry" /> recording that it
    /// happened, in a single transaction so the record and the removal cannot
    /// come apart.
    ///
    /// It guarantees a state (no data for this patient) rather than acting on a
    /// precondition: a patient the service holds nothing about is a valid erasure
    /// of zero rows, still logged, so an erasure request always has an auditable
    /// answer and running it twice is safe.
    ///
    /// The log is the one thing erasure does not erase — its subject is gone, so
    /// the act has to be accountable on its own. Chunks are deleted before
    /// ingestions because a chunk points at its ingestion; the erasure_log row
    /// references neither, so it survives both.
    /// </summary>
    public Task<(int IngestionsErased, int ChunksErased)> ErasePatientDataAsync(
        string patientId, string erasedBy, CancellationToken ct = default) =>
        db.InTransactionAsync(async innerCt =>
        {
            // Chunks and analyte rows first: each carries a foreign key to its
            // ingestion, so the ingestion rows cannot go until nothing points at
            // them. Analyte rows are a patient's clinical numbers and are erased
            // with everything else (T31).
            var chunksErased = await db.Chunks.Where(c => c.PatientId == patientId).ExecuteDeleteAsync(innerCt);
            await db.AnalyteResults.Where(a => a.PatientId == patientId).ExecuteDeleteAsync(innerCt);
            var ingestionsErased = await db.Ingestions.Where(i => i.PatientId == patientId).ExecuteDeleteAsync(innerCt);

            db.ErasureLog.Add(new ErasureLogEntry
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                ErasedBy = erasedBy,
                ErasedAt = DateTimeOffset.UtcNow,
                IngestionsErased = ingestionsErased,
                ChunksErased = chunksErased,
            });
            await db.SaveChangesAsync(innerCt);

            return (ingestionsErased, chunksErased);
        }, ct);

    /// <summary>
    /// Un-ingests a Document: in one transaction its chunks are deleted, its raw
    /// payload is scrubbed, and its live Ingestion becomes a Deleted tombstone
    /// naming who removed it and when. The canonical case is a wrong-patient
    /// upload, so removal has to be complete — nothing of the clinical content
    /// left behind — while the tombstone keeps the removal accountable.
    ///
    /// A Document mid-ingest is <see cref="UnIngestOutcome.InFlight"/>: its chunk
    /// set is not settled and a worker is writing it, so the run has to reach a
    /// terminal state before it can be removed. A Document with no live version —
    /// an unknown id, or one already un-ingested — is
    /// <see cref="UnIngestOutcome.NotFound"/>.
    ///
    /// The tombstone deliberately survives: a document that simply vanished would
    /// be worse than the mistake un-ingest exists to correct. Erasing even the
    /// tombstone is GDPR Erasure's job, guarded by its own admin secret.
    /// </summary>
    public Task<(UnIngestOutcome Outcome, DateTimeOffset? DeletedAt)> TryUnIngestAsync(
        string documentId, string removedBy, CancellationToken ct = default) =>
        db.InTransactionAsync(async innerCt =>
        {
            // In-flight is checked first and across every row for the document: a
            // correction can be Queued while the previous version is still
            // Completed, and removing one out from under the other mid-run is the
            // race this avoids. The check writes nothing, so returning here just
            // ends an empty transaction.
            var inFlight = await db.Ingestions
                .Where(i => i.DocumentId == documentId && (i.Status == "Queued" || i.Status == "Processing"))
                .AnyAsync(innerCt);
            if (inFlight)
                return (UnIngestOutcome.InFlight, (DateTimeOffset?)null);

            // Guarded on Completed, so two deletes racing settle to one, and a
            // document with no live version is left untouched rather than
            // tombstoned twice. There is at most one Completed row per document —
            // supersede demotes the old one as the new one lands — so this flips
            // exactly it. A no-match changes nothing, so the early return again
            // commits an empty transaction.
            var deletedAt = DateTimeOffset.UtcNow;
            var tombstoned = await db.Ingestions
                .Where(i => i.DocumentId == documentId && i.Status == "Completed")
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(i => i.Status, "Deleted")
                        .SetProperty(i => i.DeletedBy, removedBy)
                        .SetProperty(i => i.DeletedAt, deletedAt)
                        .SetProperty(i => i.Payload, (string?)null)
                        .SetProperty(i => i.UpdatedAt, deletedAt),
                    innerCt);
            if (tombstoned == 0)
                return (UnIngestOutcome.NotFound, null);

            // The chunks carry the same assembled id, so this removes exactly the
            // live version's chunks and no sibling's — atomically with the flip
            // above. A LabReport's analyte rows carry the same id and go with them,
            // so un-ingest leaves nothing of the document's clinical content (T31).
            await db.Chunks.Where(c => c.DocumentId == documentId).ExecuteDeleteAsync(innerCt);
            await db.AnalyteResults.Where(a => a.DocumentId == documentId).ExecuteDeleteAsync(innerCt);

            return (UnIngestOutcome.Deleted, deletedAt);
        }, ct);
}
