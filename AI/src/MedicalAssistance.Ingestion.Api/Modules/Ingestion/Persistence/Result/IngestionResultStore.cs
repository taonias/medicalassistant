using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace MedicalAssistance.Ingestion.Api.Ingestions;

/// <summary>A fully assembled, embedded chunk handed from a strategy to the store for the atomic commit.</summary>
/// <param name="Index">Ordinal of the chunk within its document.</param>
/// <param name="Kind">What the text is: dialog or summary.</param>
/// <param name="VerbatimText">The chunk text, verbatim from the source (or the labeled AI summary).</param>
/// <param name="ContextBlurb">LLM-written retrieval context; null for summary chunks.</param>
/// <param name="SourceRefJson">Type-specific provenance JSON (line range for transcripts).</param>
/// <param name="Embedding">The pgvector embedding.</param>
public sealed record ChunkToStore(
    int Index,
    string Kind,
    string VerbatimText,
    string? ContextBlurb,
    string? SourceRefJson,
    Vector Embedding);

/// <summary>
/// The chunking quality of one document, handed to the store to commit alongside
/// its chunks (T35). Built by <see cref="DocumentChunkCommitter"/> from the chunk
/// set actually stored, so the report can never describe a chunk shape that was
/// not the one committed.
/// </summary>
/// <param name="ChunkCount">How many chunks were stored, the summary chunk included.</param>
/// <param name="TokenCounts">Estimated tokens of every stored chunk, in chunk order.</param>
/// <param name="TotalTokens">Total estimated tokens across all stored chunks.</param>
/// <param name="MinTokens">Smallest chunk's estimated tokens.</param>
/// <param name="MaxTokens">Largest chunk's estimated tokens.</param>
/// <param name="GuardrailMerges">Sub-floor fragments the guardrails merged (0 for a deterministic strategy).</param>
/// <param name="GuardrailSplits">Extra chunks the guardrails produced by splitting (0 for a deterministic strategy).</param>
/// <param name="CorrectiveRetryFired">Whether the chunking agent's corrective retry fired.</param>
public sealed record QualityReportToStore(
    int ChunkCount,
    int[] TokenCounts,
    int TotalTokens,
    int MinTokens,
    int MaxTokens,
    int GuardrailMerges,
    int GuardrailSplits,
    bool CorrectiveRetryFired);

/// <summary>
/// What an Ingestion run concludes with: the atomic success commit (chunks,
/// analytes, quality report, Completed flip) and the failure path, which also
/// stages the cross-service outbox event a terminal transcript failure owes
/// the backend.
/// </summary>
internal sealed class IngestionResultStore(IngestionDbContext db)
{
    /// <summary>
    /// The chunking quality report of one Ingestion, or null when none exists yet —
    /// the ingestion is unknown, still running, or failed before it committed (T35).
    /// </summary>
    public Task<IngestionQualityReportView?> GetQualityReportAsync(Guid id, CancellationToken ct = default) =>
        db.IngestionQualityReports.AsNoTracking()
            .Where(q => q.IngestionId == id)
            .Select(q => new IngestionQualityReportView
            {
                IngestionId = q.IngestionId,
                ChunkCount = q.ChunkCount,
                TokenCounts = q.TokenCounts,
                TotalTokens = q.TotalTokens,
                MinTokens = q.MinTokens,
                MaxTokens = q.MaxTokens,
                // Integer division is deliberate: a token estimate is already a
                // whole-number heuristic, so a fractional mean would imply a
                // precision the count does not have. Guarded against the zero-chunk
                // case, which a committed report never is but a reader should not
                // divide by.
                MeanTokens = q.ChunkCount == 0 ? 0 : q.TotalTokens / q.ChunkCount,
                GuardrailMerges = q.GuardrailMerges,
                GuardrailSplits = q.GuardrailSplits,
                CorrectiveRetryFired = q.CorrectiveRetryFired,
                CreatedAt = q.CreatedAt,
            })
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// The atomic commit of an Ingestion: any superseded version of the document
    /// is removed, all new chunks are written, and the status becomes Completed —
    /// in one transaction, so nothing is ever partially visible (ADR-0003).
    ///
    /// When this is a Correction, retrieval goes straight from the old version to
    /// the new one: it can never see both versions of a transcript at once, and
    /// never sees the document missing in between.
    /// </summary>
    public Task CompleteWithChunksAsync(
        Guid ingestionId, string documentId, IngestionRequest request, IReadOnlyList<ChunkToStore> chunks,
        int? instructionVersion, string? chatModel, string? embeddingModel,
        IReadOnlyList<VerifiedAnalyte>? analytes, bool? analytesExtracted, string? documentSummary,
        QualityReportToStore qualityReport,
        CancellationToken ct = default) =>
        db.InTransactionAsync(async innerCt =>
        {
            var ingestion = await db.Ingestions.FirstAsync(i => i.Id == ingestionId, innerCt);
            await SupersedePreviousVersionAsync(ingestionId, documentId, request, innerCt);

            db.Chunks.AddRange(chunks.Select(chunk => new Chunk
            {
                Id = Guid.NewGuid(),
                IngestionId = ingestionId,
                ChunkIndex = chunk.Index,
                DocumentId = documentId,
                DocumentType = request.DocumentType,
                PatientId = request.PatientId,
                DoctorId = request.DoctorId,
                SessionId = request.SessionId,
                DocumentDate = request.SessionDate,
                Language = request.Language,
                ChunkKind = chunk.Kind,
                SourceRef = chunk.SourceRefJson,
                VerbatimText = chunk.VerbatimText,
                ContextBlurb = chunk.ContextBlurb,
                Embedding = chunk.Embedding,
                EmbeddingModel = embeddingModel,
            }));

            // Analyte rows commit in the same transaction as the chunks (T31): a
            // document never holds a chunk set without its verified analytes, or the
            // reverse, and a Correction's supersede removes both together below.
            if (analytes is not null)
                db.AnalyteResults.AddRange(analytes.Select(analyte => new AnalyteResult
                {
                    Id = Guid.NewGuid(),
                    IngestionId = ingestionId,
                    DocumentId = documentId,
                    PatientId = request.PatientId,
                    DoctorId = request.DoctorId,
                    CanonicalName = analyte.CanonicalName,
                    VerbatimName = analyte.VerbatimName,
                    Value = analyte.Value,
                    Unit = analyte.Unit,
                    ReferenceRange = analyte.ReferenceRange,
                    Flag = analyte.Flag,
                    TableIndex = analyte.TableIndex,
                    RowIndex = analyte.RowIndex,
                }));

            // The quality report commits in the same transaction as the chunks and
            // the Completed flip (T35), so a completed ingestion always has a report
            // and a report never describes an ingestion that did not finish. Only a
            // Failed ingestion is ever rerun, and a failed run never reached here, so
            // one ingestion id inserts exactly one report — insert, not upsert.
            db.IngestionQualityReports.Add(new IngestionQualityReport
            {
                IngestionId = ingestionId,
                ChunkCount = qualityReport.ChunkCount,
                TokenCounts = qualityReport.TokenCounts,
                TotalTokens = qualityReport.TotalTokens,
                MinTokens = qualityReport.MinTokens,
                MaxTokens = qualityReport.MaxTokens,
                GuardrailMerges = qualityReport.GuardrailMerges,
                GuardrailSplits = qualityReport.GuardrailSplits,
                CorrectiveRetryFired = qualityReport.CorrectiveRetryFired,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            ingestion.Status = "Completed";
            ingestion.InstructionVersion = instructionVersion;
            ingestion.ChatModel = chatModel;
            ingestion.AnalytesExtracted = analytesExtracted;
            ingestion.Summary = documentSummary;
            ingestion.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(innerCt);
        }, ct);

    /// <summary>Moves an Ingestion to Failed, recording why — an honest, retriable failure (never silent).</summary>
    public Task MarkFailedAsync(Guid id, string errorMessage, CancellationToken ct = default) =>
        db.InTransactionAsync(async innerCt =>
        {
            await db.Ingestions.Where(i => i.Id == id).ExecuteUpdateAsync(setters => setters
                .SetProperty(i => i.Status, "Failed")
                .SetProperty(i => i.ErrorMessage, errorMessage)
                .SetProperty(i => i.UpdatedAt, DateTimeOffset.UtcNow), innerCt);

            // Only a transcript maps to a backend consultation. Enqueue the ingestion-failed
            // event in the same transaction as the status, so the backend can reconcile the
            // consultation into a doctor-visible, retryable failure. The relay publishes it.
            var info = await db.Ingestions
                .Where(i => i.Id == id)
                .Select(i => new { i.DocumentType, i.SessionId })
                .SingleOrDefaultAsync(innerCt);

            if (info is { DocumentType: DocumentTypes.SessionTranscript, SessionId: not null } &&
                !string.IsNullOrWhiteSpace(info.SessionId))
            {
                db.IntegrationEventOutbox.Add(
                    IntegrationEventOutboxMessage.IngestionFailed(id, info.SessionId, errorMessage));
                await db.SaveChangesAsync(innerCt);
            }
        }, ct);

    /// <summary>
    /// Clears out the version of this Document that is being replaced: its chunks
    /// are deleted and its Ingestion is marked Superseded.
    ///
    /// The status matters as much as the delete. A Correction leaves the earlier
    /// ingestion with no chunks at all, so leaving it as Completed would let the
    /// dedup rule answer "already ingested" about text that no longer exists —
    /// and a re-upload of the original would be silently dropped. Superseded is
    /// the honest record: this ran, and this is no longer what the store holds.
    /// </summary>
    private async Task SupersedePreviousVersionAsync(
        Guid ingestionId, string documentId, IngestionRequest request, CancellationToken ct)
    {
        // The patient is inside documentId now, so this predicate is redundant.
        // It stays because the statement deletes clinical data: if an id is ever
        // built wrongly, the blast radius is confined to the patient it names
        // rather than reaching whoever else happens to match.
        await db.Chunks
            .Where(c => c.DocumentId == documentId && c.PatientId == request.PatientId)
            .ExecuteDeleteAsync(ct);

        // A LabReport's analyte rows are part of the version being replaced, so they
        // go with its chunks — otherwise a corrected report would keep the previous
        // version's numbers in the analyte table (T31).
        await db.AnalyteResults
            .Where(a => a.DocumentId == documentId && a.PatientId == request.PatientId)
            .ExecuteDeleteAsync(ct);

        await db.Ingestions
            .Where(i => i.Id != ingestionId
                        && i.Status == "Completed"
                        && i.DocumentId == documentId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(i => i.Status, "Superseded")
                    .SetProperty(i => i.UpdatedAt, DateTimeOffset.UtcNow),
                ct);
    }
}
