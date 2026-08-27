using MedicalAssistance.Ingestion.Api.DocumentLifecycle;

namespace MedicalAssistance.Ingestion.Api.Ingestions;

/// <summary>
/// All database access for the ingestion pipeline. Owns the lifecycle of an
/// Ingestion record and the atomic chunk commit; nothing outside this class
/// writes to the database.
///
/// A facade (R25) over five focused collaborators, split by responsibility:
/// <see cref="IngestionIntakeStore"/> (accepting a submission and its dedup
/// checks), <see cref="IngestionLeasingStore"/> (claiming, retrying, and
/// finding unfinished work), <see cref="IngestionDocumentCatalogStore"/>
/// (read-side views plus the patient summary), <see cref="IngestionResultStore"/>
/// (the terminal commit — success and failure), and
/// <see cref="IngestionCleanupStore"/> (un-ingest and GDPR erasure). This
/// class's own public surface — every method below — is unchanged from
/// before the split, so every existing caller (DI registration included)
/// keeps working without modification.
/// </summary>
public sealed class IngestionStore(IngestionDbContext db)
{
    private readonly IngestionIntakeStore _intake = new(db);
    private readonly IngestionLeasingStore _leasing = new(db);
    private readonly IngestionDocumentCatalogStore _catalog = new(db);
    private readonly IngestionResultStore _result = new(db);
    private readonly IngestionCleanupStore _cleanup = new(db);

    /// <inheritdoc cref="IngestionIntakeStore.FindInFlightAsync" />
    public Task<Guid?> FindInFlightAsync(IngestionRequest request, CancellationToken ct = default) =>
        _intake.FindInFlightAsync(request, ct);

    /// <inheritdoc cref="IngestionIntakeStore.FindIdenticalAsync" />
    public Task<IdenticalIngestion?> FindIdenticalAsync(IngestionRequest request, CancellationToken ct = default) =>
        _intake.FindIdenticalAsync(request, ct);

    /// <inheritdoc cref="IngestionIntakeStore.FindSameContentElsewhereAsync" />
    public Task<IdenticalIngestion?> FindSameContentElsewhereAsync(
        IngestionRequest request, CancellationToken ct = default) =>
        _intake.FindSameContentElsewhereAsync(request, ct);

    /// <inheritdoc cref="IngestionLeasingStore.TryRetryAsync" />
    public Task<(RetryOutcome Outcome, string? CurrentStatus)> TryRetryAsync(
        Guid id, CancellationToken ct = default) =>
        _leasing.TryRetryAsync(id, ct);

    /// <inheritdoc cref="IngestionIntakeStore.CreateQueuedAsync" />
    public Task<Guid> CreateQueuedAsync(IngestionRequest request, CancellationToken ct = default) =>
        _intake.CreateQueuedAsync(request, ct);

    /// <inheritdoc cref="IngestionDocumentCatalogStore.GetIdentityAsync" />
    public Task<IngestionIdentity?> GetIdentityAsync(Guid id, CancellationToken ct = default) =>
        _catalog.GetIdentityAsync(id, ct);

    /// <inheritdoc cref="IngestionDocumentCatalogStore.GetStatusAsync" />
    public Task<IngestionStatus?> GetStatusAsync(Guid id, CancellationToken ct = default) =>
        _catalog.GetStatusAsync(id, ct);

    /// <inheritdoc cref="IngestionDocumentCatalogStore.ListForDoctorAsync" />
    public Task<List<IngestionSummary>> ListForDoctorAsync(
        string doctorId, bool activeOnly, int limit, CancellationToken ct = default) =>
        _catalog.ListForDoctorAsync(doctorId, activeOnly, limit, ct);

    /// <inheritdoc cref="IngestionDocumentCatalogStore.ListPatientDocumentsAsync" />
    public Task<List<PatientDocument>> ListPatientDocumentsAsync(
        string patientId, string? doctorId = null, CancellationToken ct = default) =>
        _catalog.ListPatientDocumentsAsync(patientId, doctorId, ct);

    /// <inheritdoc cref="IngestionDocumentCatalogStore.ListCompletedDocumentSummariesAsync" />
    public Task<List<PatientDocumentSummary>> ListCompletedDocumentSummariesAsync(
        string patientId, CancellationToken ct = default) =>
        _catalog.ListCompletedDocumentSummariesAsync(patientId, ct);

    /// <inheritdoc cref="IngestionDocumentCatalogStore.GetPatientSummaryAsync" />
    public Task<PatientSummary?> GetPatientSummaryAsync(string patientId, CancellationToken ct = default) =>
        _catalog.GetPatientSummaryAsync(patientId, ct);

    /// <inheritdoc cref="IngestionDocumentCatalogStore.UpsertPatientSummaryAsync" />
    public Task UpsertPatientSummaryAsync(
        string patientId, string summary, int documentCount, string? chatModel, int? instructionVersion,
        CancellationToken ct = default) =>
        _catalog.UpsertPatientSummaryAsync(patientId, summary, documentCount, chatModel, instructionVersion, ct);

    /// <inheritdoc cref="IngestionResultStore.GetQualityReportAsync" />
    public Task<IngestionQualityReportView?> GetQualityReportAsync(Guid id, CancellationToken ct = default) =>
        _result.GetQualityReportAsync(id, ct);

    /// <inheritdoc cref="IngestionIntakeStore.LoadRequestAsync" />
    public Task<IngestionRequest> LoadRequestAsync(Guid id, CancellationToken ct = default) =>
        _intake.LoadRequestAsync(id, ct);

    /// <inheritdoc cref="IngestionLeasingStore.TryClaimAsync" />
    public Task<ClaimOutcome> TryClaimAsync(Guid id, int maxAttempts, CancellationToken ct = default) =>
        _leasing.TryClaimAsync(id, maxAttempts, ct);

    /// <inheritdoc cref="IngestionLeasingStore.FindUnfinishedAsync" />
    public Task<List<Guid>> FindUnfinishedAsync(CancellationToken ct = default) =>
        _leasing.FindUnfinishedAsync(ct);

    /// <inheritdoc cref="IngestionResultStore.MarkFailedAsync" />
    public Task MarkFailedAsync(Guid id, string errorMessage, CancellationToken ct = default) =>
        _result.MarkFailedAsync(id, errorMessage, ct);

    /// <inheritdoc cref="IngestionCleanupStore.ErasePatientDataAsync" />
    public Task<(int IngestionsErased, int ChunksErased)> ErasePatientDataAsync(
        string patientId, string erasedBy, CancellationToken ct = default) =>
        _cleanup.ErasePatientDataAsync(patientId, erasedBy, ct);

    /// <inheritdoc cref="IngestionCleanupStore.TryUnIngestAsync" />
    public Task<(UnIngestOutcome Outcome, DateTimeOffset? DeletedAt)> TryUnIngestAsync(
        string documentId, string removedBy, CancellationToken ct = default) =>
        _cleanup.TryUnIngestAsync(documentId, removedBy, ct);

    /// <inheritdoc cref="IngestionResultStore.CompleteWithChunksAsync" />
    public Task CompleteWithChunksAsync(
        Guid ingestionId, string documentId, IngestionRequest request, IReadOnlyList<ChunkToStore> chunks,
        int? instructionVersion, string? chatModel, string? embeddingModel,
        IReadOnlyList<VerifiedAnalyte>? analytes, bool? analytesExtracted, string? documentSummary,
        QualityReportToStore qualityReport,
        CancellationToken ct = default) =>
        _result.CompleteWithChunksAsync(
            ingestionId, documentId, request, chunks, instructionVersion, chatModel, embeddingModel,
            analytes, analytesExtracted, documentSummary, qualityReport, ct);
}
