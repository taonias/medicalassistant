using MedicalAssistance.Ingestion.Api.DocumentLifecycle;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistance.Ingestion.Api.Ingestions;

/// <summary>
/// An existing Ingestion of the same Document identity carrying byte-for-byte
/// identical content — the input to the dedup decision. Lifecycle strings stay
/// inside the store; callers ask what the outcome was, not how it is spelled.
/// </summary>
/// <param name="Id">Identifier of the existing Ingestion.</param>
/// <param name="Status">Its lifecycle state.</param>
public sealed record IdenticalIngestion(Guid Id, string Status)
{
    /// <summary>The identical content is already ingested; re-running it would only reproduce what exists.</summary>
    public bool Succeeded => Status == "Completed";

    /// <summary>The identical content failed to ingest; re-posting it is a retry, not a duplicate.</summary>
    public bool Failed => Status == "Failed";
}

/// <summary>
/// Accepting a submitted Document: dedup checks against what's already in
/// flight or already ingested, durably queuing a new one, and reloading a
/// stored submission for processing or rerun.
/// </summary>
internal sealed class IngestionIntakeStore(IngestionDbContext db)
{
    private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Finds an Ingestion that has been accepted but has not finished — Queued
    /// or Processing — for this Document identity, or for this exact content
    /// filed anywhere else. Returns null when nothing is in flight.
    ///
    /// Two workers running the same document at once would race to write its
    /// chunk set, and neither dedup nor Correction can settle a document that
    /// has not landed yet, so the second submission is refused until the first
    /// reaches a terminal state.
    ///
    /// The document is matched by its assembled id, which carries the whole
    /// per-type key including the patient: whatever a type's identity is, two
    /// submissions are the same document exactly when their ids are equal, and one
    /// patient's upload can never block another's.
    /// </summary>
    public Task<Guid?> FindInFlightAsync(IngestionRequest request, CancellationToken ct = default)
    {
        var (_, contentHash) = SerializeAndHash(request);
        var documentId = DocumentIdentity.For(request);
        return db.Ingestions.AsNoTracking()
            .Where(i => (i.Status == "Queued" || i.Status == "Processing")
                        && (i.DocumentId == documentId || i.ContentHash == contentHash))
            .OrderBy(i => i.CreatedAt)
            .Select(i => (Guid?)i.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Finds the most recent Ingestion for the same Document identity whose
    /// submitted content is byte-for-byte identical, or null when this content
    /// has never been submitted for this identity. Same identity with different
    /// content is a Correction, not a duplicate, and is not reported here.
    ///
    /// The document is matched by its assembled id, which is the whole per-type
    /// key. The hash covers the identity's parts too, so pairing the two was
    /// already correct — but the id states the key outright rather than leaving it
    /// to an argument, and it is the one match every type shares.
    /// </summary>
    public Task<IdenticalIngestion?> FindIdenticalAsync(IngestionRequest request, CancellationToken ct = default)
    {
        var (_, contentHash) = SerializeAndHash(request);
        var documentId = DocumentIdentity.For(request);
        return db.Ingestions.AsNoTracking()
            .Where(i => i.DocumentId == documentId && i.ContentHash == contentHash)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new IdenticalIngestion(i.Id, i.Status))
            .FirstOrDefaultAsync(ct)!;
    }

    /// <summary>
    /// Finds a completed Ingestion of this exact content filed under a different
    /// identity — the same recording re-uploaded as a new session or a new
    /// sequence number. Ingesting it again would put the same passages in the
    /// patient's record twice and let the chat quote one encounter as if it were
    /// two, so it is skipped. Only completed ingestions qualify: content that is
    /// still in flight or that failed has nothing to deduplicate against.
    /// </summary>
    public Task<IdenticalIngestion?> FindSameContentElsewhereAsync(
        IngestionRequest request, CancellationToken ct = default)
    {
        var (_, contentHash) = SerializeAndHash(request);
        return db.Ingestions.AsNoTracking()
            .Where(i => i.ContentHash == contentHash && i.Status == "Completed")
            .OrderBy(i => i.CreatedAt)
            .Select(i => new IdenticalIngestion(i.Id, i.Status))
            .FirstOrDefaultAsync(ct)!;
    }

    /// <summary>Durably records a submitted Document as a Queued Ingestion (with content hash and raw payload) and returns its id.</summary>
    public async Task<Guid> CreateQueuedAsync(IngestionRequest request, CancellationToken ct = default)
    {
        var (payload, contentHash) = SerializeAndHash(request);
        var record = new IngestionRecord
        {
            Id = Guid.NewGuid(),
            DocumentId = DocumentIdentity.For(request),
            DocumentType = request.DocumentType,
            DoctorId = request.DoctorId,
            PatientId = request.PatientId,
            SessionId = request.SessionId,
            SequenceNumber = request.SequenceNumber,
            DocumentDate = request.SessionDate,
            Status = "Queued",
            ContentHash = contentHash,
            Payload = payload,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Ingestions.Add(record);
        await db.SaveChangesAsync(ct);
        return record.Id;
    }

    /// <summary>Reloads the original submitted payload of an Ingestion — the input for processing and rerun-from-scratch.</summary>
    public async Task<IngestionRequest> LoadRequestAsync(Guid id, CancellationToken ct = default)
    {
        var payload = await db.Ingestions.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => i.Payload)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Ingestion {id} has no stored payload.");
        return JsonSerializer.Deserialize<IngestionRequest>(payload, PayloadJson)
            ?? throw new InvalidOperationException($"Ingestion {id} payload could not be deserialized.");
    }

    /// <summary>
    /// The submitted payload, plus a hash of what the document actually *is*.
    ///
    /// The hash deliberately excludes the filing identifiers — a transcript's
    /// <see cref="IngestionRequest.SessionId"/> and
    /// <see cref="IngestionRequest.SequenceNumber"/>, and a note's
    /// <see cref="IngestionRequest.NoteId"/> — because where a document is filed is
    /// not what it contains, so the same content re-uploaded under a fresh
    /// identifier is still recognised as already ingested. Everything else is in
    /// scope: the patient and doctor (so one patient's document can never dedup
    /// against another's), the clinical date and language (so correcting them
    /// re-ingests), and the body itself — a transcript's Transcript, a note's Text,
    /// or a report's PdfContent, whichever the type carries.
    /// </summary>
    private static (string Payload, string ContentHash) SerializeAndHash(IngestionRequest request)
    {
        var payload = JsonSerializer.Serialize(request, PayloadJson);
        var content = JsonSerializer.Serialize(
            new
            {
                request.DocumentType,
                request.PatientId,
                request.DoctorId,
                request.SessionDate,
                request.Language,
                request.Transcript,
                request.Text,
                request.PdfContent,
                request.ImageLink,
            },
            PayloadJson);
        return (payload, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))));
    }
}
