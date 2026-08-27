# Medical Assistant

A doctor-facing clinical assistant: a doctor records or uploads a consultation, it's transcribed, structured into clinical data, and made searchable — so the doctor can review it, edit it, and ask grounded, cited questions about a patient's record.

The system is three independently deployable applications plus a shared event bus, coordinated but not merged:

```
Doctor's browser
      │
      ▼
 Frontend (React SPA)
      │  HTTP + SignalR
      ▼
 Backend API  ──RabbitMQ──▶  Transcription Worker ──▶ Azure Speech
      │  HTTP                        │
      │                       (writes Transcript back via events)
      ▼
 Clinical Knowledge service ──▶ its own PostgreSQL/pgvector store
```

Backend and Clinical Knowledge each own their own database; RabbitMQ carries facts between services, never clinical content itself.

## Start here

| If you want to... | Read |
|---|---|
| Run the whole stack locally | [HOW-TO-RUN.md](HOW-TO-RUN.md) |
| Understand the domain — Patients, Consultations, Transcripts, Documents, Grounded Answers | [CONTEXT-MAP.md](CONTEXT-MAP.md) |
| Understand the runtime architecture — deployables, data ownership, who owns what | [docs/architecture/system-map.md](docs/architecture/system-map.md) |
| Know what's a confirmed defect vs. a design trade-off vs. still open | [docs/known-issues/refactor-baseline.md](docs/known-issues/refactor-baseline.md) |

## Project components

| Component | What it is | Start here |
|---|---|---|
| [`frontend/`](frontend/) | React 19 + TypeScript SPA — the doctor's browser interface | [frontend/README.md](frontend/README.md) |
| [`backend/`](backend/) | .NET 8 API — auth, patients, consultations, transcripts, structured data, chat; owns `MedicalAssistantDb` | [backend/README.md](backend/README.md) |
| [`clinical-knowledge/`](clinical-knowledge/) | .NET 10 service — ingests clinical documents, retrieval, grounded chat; owns its own `ai_med` pgvector database | [clinical-knowledge/README.md](clinical-knowledge/README.md) |
| [`ops/`](ops/) | Deployment, database init, and RabbitMQ provisioning | [ops/deploy/DEPLOYMENT.md](ops/deploy/DEPLOYMENT.md) |
| [`docs/`](docs/) | Canonical, source-verified architecture documentation (current state) | [docs/architecture/system-map.md](docs/architecture/system-map.md) |
| [`project-docs/`](project-docs/) | Historical design records and investigation archive (see below) | [project-docs/PROJECT_SUMMARY.md](project-docs/PROJECT_SUMMARY.md) |
| [`tests/`](tests/) | Cross-service acceptance and public-contract gates | [tests/MedicalAssistant.AcceptanceTests/README.md](tests/MedicalAssistant.AcceptanceTests/README.md) |
| [`scripts/`](scripts/), root `*.ps1` | Dev/release scripts (`start.ps1`, `stop.ps1`, `start-rabbitmq.ps1`) | [HOW-TO-RUN.md](HOW-TO-RUN.md) |

Backend and Transcription Worker are separate deployables from the same `backend/` solution; the Worker turns uploaded audio into a Transcript over RabbitMQ rather than being called directly.

## Architecture and domain documentation (`docs/`)

The current, actively-maintained documentation set. If a design doc elsewhere in the repo disagrees with something here, this is right.

- **[System map](docs/architecture/system-map.md)** — services, data ownership, developer ownership seams.
- **[Observable interface baseline](docs/architecture/observable-interface-baseline.md)** — the frozen HTTP/SignalR contract snapshot a script verifies hasn't silently drifted.
- **Contexts** — the three bounded domain contexts, each with its own glossary:
  [Care Workflow](docs/contexts/care-workflow/CONTEXT.md) · [Consultation Processing](docs/contexts/consultation-processing/CONTEXT.md) · [Clinical Knowledge](docs/contexts/clinical-knowledge/CONTEXT.md)
- **[Module READMEs](docs/modules/README.md)** — ownership, public interface, invariants, and tests for each of the 10 capability areas:

  | Module | Owns |
  |---|---|
  | [Frontend Platform](docs/modules/frontend-platform.md) | Bootstrap, routing, transport/query/media/realtime adapters, design system |
  | [Identity & Preferences](docs/modules/identity-and-preferences.md) | Session, roles, profile, password, theme |
  | [Patient Workspace](docs/modules/patient-workspace.md) | Patient directory, details, history, recent-patients |
  | [Consultation Lifecycle](docs/modules/consultation-lifecycle.md) | Create/assign/upload/status/retry/delete a consultation |
  | [Clinical Record](docs/modules/clinical-record.md) | Transcript review/editing, structured medical data, doctor notes |
  | [Consultation Processing](docs/modules/consultation-processing.md) | Turning uploaded audio into a Transcript |
  | [Durable Messaging](docs/modules/durable-messaging.md) | Event envelopes, RabbitMQ topology, outbox/inbox mechanics |
  | [Clinical Knowledge](docs/modules/clinical-knowledge.md) | Ingestion, retrieval, grounded chat |
  | [Runtime & Data](docs/modules/runtime-and-data.md) | Composition roots, DbContexts, migrations, ops scripts |
  | [Cross-system Quality](docs/modules/cross-system-quality.md) | Test harnesses, architecture enforcement, baseline snapshots |

- **[Architecture Decision Records](docs/adr/README.md)** — 10 accepted decisions behind the event-bus/consultation-processing design (standalone worker, transactional outbox, at-least-once delivery, versioned contracts, and more).
- **Runbooks** — what to do when something breaks, grounded in verified source behavior, not guessed:
  [Messaging and recovery](docs/runbooks/messaging-and-recovery.md) · [Patient Workspace](docs/runbooks/patient-workspace.md) · [Consultation Lifecycle](docs/runbooks/consultation-lifecycle.md) · [Clinical Knowledge](docs/runbooks/clinical-knowledge.md)
- **[Known-issue ledger](docs/known-issues/refactor-baseline.md)** — the K/Q-numbered list of confirmed defects, risks, and characterization gaps, each with a severity and a proposed fix.
- **[Agent workflow docs](docs/agents/domain.md)** — reading order for the domain, [issue-tracker convention](docs/agents/issue-tracker.md), and [triage labels](docs/agents/triage-labels.md), for anyone (human or AI agent) about to change code here.

## Clinical Knowledge service documentation (`clinical-knowledge/`)

The Clinical Knowledge service is a self-contained deployable with its own fuller documentation tree, since it started as a separate project:

- [clinical-knowledge/README.md](clinical-knowledge/README.md) — structure, endpoints, how to run it locally.
- [clinical-knowledge/CONTEXT.md](clinical-knowledge/CONTEXT.md) — its own, more detailed domain glossary.
- [clinical-knowledge/database/README.md](clinical-knowledge/database/README.md) — the pgvector container, migrations, and a destructive-reset warning.
- [clinical-knowledge/docs/adr/](clinical-knowledge/docs/adr/) — 12 accepted decisions (pgvector for state+vectors, LLM-boundaries-only chunking, atomic rerun-from-scratch ingestion, grounded-answer verification, and more).
- PRDs: [Clinical Document Ingestion](clinical-knowledge/docs/prd/0001-clinical-document-ingestion-service.md) · [Retrieval & Grounded Chat](clinical-knowledge/docs/prd/0002-retrieval-and-grounded-chat.md)
- Design records: [Ingestion pipeline](clinical-knowledge/docs/ingestion-pipeline-design.md) · [Retrieval & chat pipeline](clinical-knowledge/docs/retrieval-and-chat-design.md)

## Backend-specific documentation (`backend/`)

- [backend/README.md](backend/README.md) — Clean Architecture layering, how the layers map to `docs/modules/`.
- [backend/docs/CHAT_CONVERSATIONS.md](backend/docs/CHAT_CONVERSATIONS.md) — the doctor↔AI chat feature: ownership, durable history, live progress, citations.

`backend/docs/AI_MODULE_API.md` documents an older, unimplemented `/v1/*` contract with a "Python AI Module" that was never built this way — `backend/README.md` flags it as legacy; the real integration is `clinical-knowledge/`.

## Testing

- [tests/MedicalAssistant.AcceptanceTests/README.md](tests/MedicalAssistant.AcceptanceTests/README.md) — full-system acceptance gates, plus the fast [Contracts/](tests/MedicalAssistant.AcceptanceTests/Contracts/README.md) subset that freezes the HTTP/SignalR surface without starting Docker.
- [backend/test/MedicalAssistant.Persistence.IntegrationTests/README.md](backend/test/MedicalAssistant.Persistence.IntegrationTests/README.md) — PostgreSQL-backed characterization tests (Testcontainers).
- [clinical-knowledge/tests/MedicalAssistance.Ingestion.Api.Tests/README.md](clinical-knowledge/tests/MedicalAssistance.Ingestion.Api.Tests/README.md) — the whole Clinical Knowledge service tested in-process against a real pgvector container.
- [frontend/e2e/README.md](frontend/e2e/README.md) — Playwright journeys that freeze user-visible behavior, grouped by capability ownership.

## Operations and deployment

- [HOW-TO-RUN.md](HOW-TO-RUN.md) — local stack via `docker-compose.yml`, ports, environment variables, startup order.
- [ops/deploy/DEPLOYMENT.md](ops/deploy/DEPLOYMENT.md) — production: a single Ubuntu VM, `docker-compose.prod.yml`, nginx + Let's Encrypt, images built locally and shipped by SFTP.
- `ops/postgres/`, `ops/rabbitmq/` — database init scripts and local RabbitMQ provisioning.

## Historical design and investigation archive (`project-docs/`)

Design documents and one-time investigations from earlier in the project's life, kept as evidence of *why* things are the way they are rather than rewritten to track the current code. Several files carry a `> Historical snapshot` banner pointing at the canonical `docs/` replacement — trust `docs/` over these where they disagree.

- [project-docs/PROJECT_SUMMARY.md](project-docs/PROJECT_SUMMARY.md) — one-page historical topology snapshot.
- [project-docs/operations/README.md](project-docs/operations/README.md) — an 11-part, point-in-time code review (product overview through current-state gaps).
- [project-docs/event bus implementations/README.md](project-docs/event%20bus%20implementations/README.md) — the still-living implementation specification for the RabbitMQ/outbox/Transcription-Worker architecture (this one *is* kept in sync with source, unlike its sibling above).
- [project-docs/e2e-alignment/README.md](project-docs/e2e-alignment/README.md) — the real-user end-to-end journey and local E2E runbook.

## Contributing / working with AI agents

- [AGENTS.md](AGENTS.md) — where to start: agent skills, issue-tracker convention, domain reading order.
- [.github/CODEOWNERS](.github/CODEOWNERS) — ownership by capability, matching the module list above.
- [.github/pull_request_template.md](.github/pull_request_template.md) — the PR checklist, including the rule that a structural PR doesn't silently fix an item from the known-issue ledger.
