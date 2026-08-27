# Clinical Knowledge

## Purpose

Ingests declared clinical Documents (Session Transcripts, Doctor Notes, Lab Reports, Imaging Reports) into a vector store and answers a doctor's questions about a patient, grounded only in evidence it can cite. The one Clinical Knowledge deployable (`clinical-knowledge/`) — its own service, own database, own ADRs.

## Public interface / seam

Authenticated HTTP, called only by the main backend (never the browser directly) — see [clinical-knowledge/README.md](../../clinical-knowledge/README.md) for the full endpoint table and how to run the service locally. Domain glossary: [docs/contexts/clinical-knowledge/CONTEXT.md](../contexts/clinical-knowledge/CONTEXT.md) (canonical) and [clinical-knowledge/CONTEXT.md](../../clinical-knowledge/CONTEXT.md) (the service's own fuller local glossary).

## Invariants

- Ingestion is atomic and rerun-from-scratch (ADR-0003) — no partial chunk sets are ever visible.
- A Correction supersedes the prior Document's chunks before the new content is ingested; retrieval never sees both versions of a Document at once (see `IngestionResultStore`'s own doc comment).
- Grounded Answers are verified against supplied evidence before release and refuse to answer beyond it (ADR-0012) — the one non-overridable rule in the whole retrieval path.
- `DocumentLifecycle` and `Ingestions` (R38) are genuinely, bidirectionally coupled at the type level — a pre-existing fact this session's namespace alignment made visible rather than created. See [ModuleBoundaryTests.cs](../../clinical-knowledge/tests/MedicalAssistance.Ingestion.Api.Tests/ModuleBoundaryTests.cs)'s own doc comment for the specifics; not fixed here — a real design decision, not a namespace move.

## Dependencies

**Owns**: ingestion, document lifecycle, retrieval, grounded answers, summaries, its own clinical database and adapters.
**Does not own**: Care Workflow authorization or patient ownership — it trusts the backend-supplied doctor/patient identifiers (ADR-0007) rather than deciding who may see what.

## Tests

`clinical-knowledge/tests/MedicalAssistance.Ingestion.Api.Tests` — 189 tests, real PostgreSQL/pgvector via Testcontainers (Docker required), including the R38 `ModuleBoundaryTests` and the R25/R10 golden characterization suite.

## Runbook & known risks

Runbook: [docs/runbooks/clinical-knowledge.md](../runbooks/clinical-knowledge.md). Known risks tracked in [docs/known-issues/refactor-baseline.md](../known-issues/refactor-baseline.md):

| ID | Finding |
|---|---|
| K26 | Erasure/un-ingest can leave stale Patient Summary records — the glossary half is resolved (R36), the code fix is not. |
| K27 | Default embedding model dimensions may not match the fixed vector schema. |
| K28 | Citation verification permits a generated answer with no citations. |
| K29 | Production retrieval confidence threshold defaults to 0.0 and appears uncalibrated. |
| K33 | Raw provider exception messages can cross a durable service boundary. |
| K34 | Chat context is not bounded at the HTTP boundary. |
