# Architecture Decision Guide

The AI service already contains twelve architecture decision records under `clinical-knowledge/docs/adr`. This page explains why each matters to the whole project. It does not replace the source ADRs and introduces no new decision.

| ADR | Decision | Operational/product significance |
| --- | --- | --- |
| [0001](../../clinical-knowledge/docs/adr/0001-postgres-pgvector-for-status-and-vectors.md) | One PostgreSQL/pgvector store for ingestion state and vectors | Corrections can be transactional; one backup/operations surface; changing vector stores would require a consistency design |
| [0002](../../clinical-knowledge/docs/adr/0002-boundaries-only-llm-chunking.md) | Model returns line boundaries, code copies source text | Reduces risk of generated changes to stored transcript text; invalid plans fail honestly |
| [0003](../../clinical-knowledge/docs/adr/0003-ingestion-is-atomic-rerun-from-scratch.md) | Atomic ingestion with full rerun after failure | No partial searchable states; raw payload retention is required and increases PHI responsibilities |
| [0004](../../clinical-knowledge/docs/adr/0004-orchestrator-is-a-deterministic-router.md) | Declared type selects a strategy | Predictable, auditable processing; untyped document classification is deferred |
| [0005](../../clinical-knowledge/docs/adr/0005-pdfs-parsed-to-text-pixels-never-ingested.md) | Document Intelligence extracts PDF text; imaging pixels excluded | Bounds scope and risk; scanned/handwritten material remains out of scope |
| [0006](../../clinical-knowledge/docs/adr/0006-analyte-extraction-llm-maps-code-copies.md) | Model maps lab cells, code copies/verifies values | Prevents generated lab numbers; incomplete analyte extraction is represented honestly |
| [0007](../../clinical-knowledge/docs/adr/0007-api-secret-auth-not-oauth-jwt.md) | Shared secret for one backend caller | Simpler internal trust boundary; requires private networking and reconsideration if more callers appear |
| [0008](../../clinical-knowledge/docs/adr/0008-agent-instructions-in-database.md) | Prompt instructions live in DB and load at startup | Tunable without redeploy; runtime edits need governance/version tracking |
| [0009](../../clinical-knowledge/docs/adr/0009-observability-otel-and-quality-report.md) | OpenTelemetry plus persisted ingestion quality | Separates operational telemetry from durable quality evidence; no patient text should leave via telemetry |
| [0010](../../clinical-knowledge/docs/adr/0010-retrieval-and-chat-here-conversations-in-backend.md) | AI service owns retrieval/answers; backend owns conversation state | Avoids duplicated identity/message storage; backend integration must supply bounded context |
| [0011](../../clinical-knowledge/docs/adr/0011-retrieval-searches-authoritative-pgvector-no-hydration.md) | Search authoritative pgvector rows directly | Patient filter and vector query happen together; no second-store reconciliation layer |
| [0012](../../clinical-knowledge/docs/adr/0012-grounded-chat-verifies-before-release-and-refuses-without-evidence.md) | Threshold, refusal, complete-generation verification, non-streaming | Prioritizes not showing unsupported text; current code fully implements label validation, not semantic claim verification |

## Decisions the integrated product still needs

This list was accurate when first written; most of it has since been resolved by the event-bus/outbox implementation and later refactor waves, so it's corrected here rather than left to mislead:

- ~~Canonical integration transport and contract between the main backend and AI service~~ — resolved: authenticated HTTP with a shared API key, per Clinical Knowledge's own [ADR-0007](../../clinical-knowledge/docs/adr/0007-api-secret-auth-not-oauth-jwt.md).
- ~~Durable event publication/outbox strategy~~ — resolved: transactional outbox and relay, per [ADR-0002](../../docs/adr/0002-transactional-outbox.md); operational mechanics in [messaging-and-recovery.md](../../docs/runbooks/messaging-and-recovery.md).
- Cross-store correction, deletion, and erasure coordination — the mechanism is decided and implemented (event-driven deletion, [ADR-0006](../../docs/adr/0006-event-driven-deletion.md)), but coordination still has known gaps: a deletion/transcript-ready race and un-ingest/erasure not regenerating the Patient Summary — tracked as findings in the [known-issue ledger](../../docs/known-issues/refactor-baseline.md), not open architectural questions.
- ~~Authentication/session model suitable for production clinicians~~ — resolved: JWT-based ASP.NET Core Identity for doctors, API-key auth for the service-to-service Clinical Knowledge boundary.
- Deployment topology — resolved: root Docker Compose locally, a separately documented production Compose/nginx/Let's Encrypt deployment. Provider region approval and formal secret-management policy remain genuinely open, but as governance/compliance decisions (see "Required governance decisions" in [08-security-privacy-and-clinical-safety.md](08-security-privacy-and-clinical-safety.md)), not unresolved architecture.
- **Clinical acceptance criteria for transcription, retrieval, refusal, and citations — still genuinely open.** Retrieval's confidence threshold defaults to an uncalibrated 0.0, and grounded-chat citation verification doesn't require an answer to cite anything at all — both are implemented mechanisms without a calibrated, complete policy behind them yet.

Once the remaining open items are actually decided with alternatives and trade-offs, they should receive a short system-level ADR.

