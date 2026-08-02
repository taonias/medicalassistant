# Architecture Decision Guide

The AI service already contains twelve architecture decision records under `AI/docs/adr`. This page explains why each matters to the whole project. It does not replace the source ADRs and introduces no new decision.

| ADR | Decision | Operational/product significance |
| --- | --- | --- |
| [0001](../../AI/docs/adr/0001-postgres-pgvector-for-status-and-vectors.md) | One PostgreSQL/pgvector store for ingestion state and vectors | Corrections can be transactional; one backup/operations surface; changing vector stores would require a consistency design |
| [0002](../../AI/docs/adr/0002-boundaries-only-llm-chunking.md) | Model returns line boundaries, code copies source text | Reduces risk of generated changes to stored transcript text; invalid plans fail honestly |
| [0003](../../AI/docs/adr/0003-ingestion-is-atomic-rerun-from-scratch.md) | Atomic ingestion with full rerun after failure | No partial searchable states; raw payload retention is required and increases PHI responsibilities |
| [0004](../../AI/docs/adr/0004-orchestrator-is-a-deterministic-router.md) | Declared type selects a strategy | Predictable, auditable processing; untyped document classification is deferred |
| [0005](../../AI/docs/adr/0005-pdfs-parsed-to-text-pixels-never-ingested.md) | Document Intelligence extracts PDF text; imaging pixels excluded | Bounds scope and risk; scanned/handwritten material remains out of scope |
| [0006](../../AI/docs/adr/0006-analyte-extraction-llm-maps-code-copies.md) | Model maps lab cells, code copies/verifies values | Prevents generated lab numbers; incomplete analyte extraction is represented honestly |
| [0007](../../AI/docs/adr/0007-api-secret-auth-not-oauth-jwt.md) | Shared secret for one backend caller | Simpler internal trust boundary; requires private networking and reconsideration if more callers appear |
| [0008](../../AI/docs/adr/0008-agent-instructions-in-database.md) | Prompt instructions live in DB and load at startup | Tunable without redeploy; runtime edits need governance/version tracking |
| [0009](../../AI/docs/adr/0009-observability-otel-and-quality-report.md) | OpenTelemetry plus persisted ingestion quality | Separates operational telemetry from durable quality evidence; no patient text should leave via telemetry |
| [0010](../../AI/docs/adr/0010-retrieval-and-chat-here-conversations-in-backend.md) | AI service owns retrieval/answers; backend owns conversation state | Avoids duplicated identity/message storage; backend integration must supply bounded context |
| [0011](../../AI/docs/adr/0011-retrieval-searches-authoritative-pgvector-no-hydration.md) | Search authoritative pgvector rows directly | Patient filter and vector query happen together; no second-store reconciliation layer |
| [0012](../../AI/docs/adr/0012-grounded-chat-verifies-before-release-and-refuses-without-evidence.md) | Threshold, refusal, complete-generation verification, non-streaming | Prioritizes not showing unsupported text; current code fully implements label validation, not semantic claim verification |

## Decisions the integrated product still needs

These are deliberately listed as unresolved rather than invented as ADRs during documentation:

- Canonical integration transport and contract between the main backend and AI service.
- Durable event publication/outbox strategy.
- Cross-store correction, deletion, and erasure coordination.
- Authentication/session model suitable for production clinicians.
- Deployment topology, provider regions, and secret management.
- Clinical acceptance criteria for transcription, retrieval, refusal, and citations.

Once one of these choices is actually made with alternatives and trade-offs, it should receive a short system-level ADR.

