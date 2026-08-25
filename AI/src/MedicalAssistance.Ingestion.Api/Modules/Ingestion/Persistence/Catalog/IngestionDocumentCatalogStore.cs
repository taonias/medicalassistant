using Microsoft.EntityFrameworkCore;

namespace MedicalAssistance.Ingestion.Api.Ingestions;

/// <summary>
/// What an Ingestion is about: which Document it concerns and who it belongs to —
/// the routing information every status event carries, because this service has no
/// idea which devices are online.
/// </summary>
/// <param name="DocumentId">
/// The Document this Ingestion is of. Assembled once by <see cref="DocumentIdentity.For"/>
/// and thereafter read from the stored column, never rebuilt from parts — different
/// Document Types put different parts in it (a note has no sequence number), so a
/// single reassembly formula could not serve them all.
/// </param>
/// <param name="DoctorId">The doctor who submitted the document.</param>
/// <param name="PatientId">The patient the document is about.</param>
/// <param name="SessionId">Session link (transcripts and session-linked notes only).</param>
public sealed record IngestionIdentity(
    string DocumentId, string DoctorId, string PatientId, string? SessionId)
{
    /// <summary>The identity of a submission that has not been stored yet.</summary>
    public static IngestionIdentity Of(IngestionRequest request) => new(
        DocumentIdentity.For(request), request.DoctorId, request.PatientId, request.SessionId);
}

/// <summary>
/// One live document as it feeds a patient's rolling summary: its type, its clinical
/// date, and the per-document summary produced at ingestion (null for a type that
/// produces none, e.g. a LabReport).
/// </summary>
/// <param name="DocumentType">The Document Type, so the overview can weight a transcript differently from a lab report.</param>
/// <param name="DocumentDate">Clinical date of the document, for ordering the timeline.</param>
/// <param name="Summary">The document's own summary, or null when its type produces none.</param>
public sealed record PatientDocumentSummary(
    string DocumentType, DateTimeOffset? DocumentDate, string? Summary);

/// <summary>
/// Read-side views of what the service holds: an Ingestion's identity and
/// status, a doctor's or patient's documents, and a patient's rolling
/// overview (read and write — the overview is regenerated per-patient, not
/// per-ingestion, so it lives here rather than beside a single Ingestion's
/// own lifecycle).
/// </summary>
internal sealed class IngestionDocumentCatalogStore(IngestionDbContext db)
{
    /// <summary>
    /// The doctor and patient an Ingestion belongs to, or null if the id is
    /// unknown. Read from the record's own columns rather than the stored
    /// payload, which carries the entire transcript and would be a wasteful
    /// thing to deserialize for two strings.
    /// </summary>
    public Task<IngestionIdentity?> GetIdentityAsync(Guid id, CancellationToken ct = default) =>
        db.Ingestions.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new IngestionIdentity(i.DocumentId, i.DoctorId, i.PatientId, i.SessionId))
            .FirstOrDefaultAsync(ct)!;

    /// <summary>Returns the lifecycle state of one Ingestion, or null if the id is unknown.</summary>
    public Task<IngestionStatus?> GetStatusAsync(Guid id, CancellationToken ct = default) =>
        db.Ingestions.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new IngestionStatus(i.Id, i.Status, i.ErrorMessage, i.Summary))
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Lists a doctor's Ingestions, newest activity first. With
    /// <paramref name="activeOnly"/> this is the resync answer — everything
    /// accepted but not yet finished — which is what a client asks for after
    /// losing its hub connection, so no events ever need replaying.
    ///
    /// Capped rather than paged: the resync list is inherently short, and a
    /// caller asking for history has no use case yet that a page cursor would
    /// serve better than a limit.
    /// </summary>
    public async Task<List<IngestionSummary>> ListForDoctorAsync(
        string doctorId, bool activeOnly, int limit, CancellationToken ct = default)
    {
        var query = db.Ingestions.AsNoTracking().Where(i => i.DoctorId == doctorId);
        if (activeOnly)
            query = query.Where(i => i.Status == "Queued" || i.Status == "Processing");

        var ingestions = await query
            .OrderByDescending(i => i.UpdatedAt)
            .Take(limit)
            .Select(i => new
            {
                i.Id, i.DocumentId, i.DocumentType, i.PatientId, i.SessionId, i.SequenceNumber,
                i.Status, i.ErrorMessage, i.CreatedAt, i.UpdatedAt,
            })
            .ToListAsync(ct);

        return ingestions
            .Select(i => new IngestionSummary
            {
                IngestionId = i.Id,
                DocumentId = i.DocumentId,
                DocumentType = i.DocumentType,
                PatientId = i.PatientId,
                SessionId = i.SessionId,
                SequenceNumber = i.SequenceNumber,
                Status = i.Status,
                ErrorMessage = i.ErrorMessage,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt,
            })
            .ToList();
    }

    /// <summary>
    /// Every Document the service holds for a patient, one row each, in the state
    /// its most recent Ingestion left it. Superseded versions and earlier failed
    /// attempts collapse into the document they belong to — a doctor counting
    /// rows here is counting transcripts, not uploads.
    ///
    /// A Document whose current state is Deleted is left out: un-ingest removes it
    /// from the record, and a doctor should not still see a document that has been
    /// taken out. Its tombstone remains for audit, but audit is not this view.
    ///
    /// <paramref name="doctorId" /> narrows the list to one doctor's documents.
    /// It is a filter and not a permission check: this service does not decide
    /// who may see what, the backend does (ADR-0007). Left null, the answer is
    /// the patient's whole record — which is a legitimate thing for the backend
    /// to ask for, and why this does not insist on a doctor.
    /// </summary>
    public async Task<List<PatientDocument>> ListPatientDocumentsAsync(
        string patientId, string? doctorId = null, CancellationToken ct = default)
    {
        var query = db.Ingestions.AsNoTracking().Where(i => i.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(doctorId))
            query = query.Where(i => i.DoctorId == doctorId);

        var ingestions = await query
            .OrderByDescending(i => i.UpdatedAt)
            .Select(i => new
            {
                i.Id, i.DocumentId, i.DocumentType, i.DoctorId, i.SessionId, i.SequenceNumber,
                i.DocumentDate, i.Status, i.ErrorMessage, i.Summary, i.UpdatedAt,
            })
            .ToListAsync(ct);

        // Bounded by one patient's care history, so collapsing to the latest per
        // document runs here rather than as a window function nobody can read. The
        // collapse key is the assembled document id — the whole per-type key — so a
        // note keyed on noteId and a transcript keyed on (session, sequence) each
        // group correctly, and two session-less notes never merge into one row the
        // way comparing their (null) session and sequence columns would.
        return ingestions
            .DistinctBy(i => i.DocumentId)
            // After the collapse, so it is the document's current state that is
            // judged: a slot whose latest version is a Deleted tombstone drops
            // out entirely rather than falling back to an older, still-live-looking
            // row beneath it.
            .Where(i => i.Status != "Deleted")
            .Select(i => new PatientDocument
            {
                DocumentId = i.DocumentId,
                DocumentType = i.DocumentType,
                SessionId = i.SessionId,
                SequenceNumber = i.SequenceNumber,
                DocumentDate = i.DocumentDate,
                Status = i.Status,
                ErrorMessage = i.ErrorMessage,
                Summary = i.Summary,
                IngestionId = i.Id,
                UpdatedAt = i.UpdatedAt,
            })
            .OrderByDescending(document => document.DocumentDate ?? document.UpdatedAt)
            .ToList();
    }

    /// <summary>
    /// The live documents that feed a patient's rolling summary: one line per
    /// currently-held document, oldest first, so the overview reads as a timeline. A
    /// Correction sets the previous version to Superseded and un-ingest to Deleted, so
    /// filtering to Completed leaves exactly the current version of each document.
    /// </summary>
    public async Task<List<PatientDocumentSummary>> ListCompletedDocumentSummariesAsync(
        string patientId, CancellationToken ct = default) =>
        await db.Ingestions.AsNoTracking()
            .Where(i => i.PatientId == patientId && i.Status == "Completed")
            .OrderBy(i => i.DocumentDate ?? i.UpdatedAt)
            .Select(i => new PatientDocumentSummary(i.DocumentType, i.DocumentDate, i.Summary))
            .ToListAsync(ct);

    /// <summary>Returns a patient's rolling overview, or null if none has been generated yet.</summary>
    public Task<PatientSummary?> GetPatientSummaryAsync(string patientId, CancellationToken ct = default) =>
        db.PatientSummaries.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PatientId == patientId, ct);

    /// <summary>
    /// Writes a patient's freshly regenerated overview, creating the row on the first
    /// ingestion and replacing it thereafter. Callers serialize regeneration per
    /// patient with an advisory lock, so this find-then-write cannot race itself into
    /// two rows for one patient.
    /// </summary>
    public async Task UpsertPatientSummaryAsync(
        string patientId, string summary, int documentCount, string? chatModel, int? instructionVersion,
        CancellationToken ct = default)
    {
        var existing = await db.PatientSummaries.FirstOrDefaultAsync(p => p.PatientId == patientId, ct);
        if (existing is null)
        {
            db.PatientSummaries.Add(new PatientSummary
            {
                PatientId = patientId,
                Summary = summary,
                DocumentCount = documentCount,
                ChatModel = chatModel,
                InstructionVersion = instructionVersion,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            existing.Summary = summary;
            existing.DocumentCount = documentCount;
            existing.ChatModel = chatModel;
            existing.InstructionVersion = instructionVersion;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
