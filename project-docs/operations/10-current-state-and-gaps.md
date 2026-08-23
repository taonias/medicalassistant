# Current State, Contradictions, and Integration Gaps

> Historical review snapshot. Several listed gaps have since been implemented. Use the [current risk baseline](../../docs/known-issues/refactor-baseline.md) for unresolved risks and the [canonical system map](../../docs/architecture/system-map.md) for current behavior.

## Executive assessment

The repository contains a credible doctor-facing workflow and a sophisticated clinical-knowledge service, but they are not one operational end-to-end product yet. The largest work is integration and production hardening, not inventing the AI design from scratch.

## Implemented and connected

- Doctor registration/login, JWT session, profile, and password update.
- Doctor-scoped patient list/create/update/detail/history.
- Consultation creation, including unassigned drafts and idempotency key.
- Browser recording, upload, playback, download, and patient attachment UI.
- PDF upload/download in the consultation workflow.
- Dashboard consultation/patient analytics and unassigned recordings.
- RabbitMQ consultation-processing publisher and broker definition.
- Azure Functions worker consuming uploaded consultation events.
- Blob retrieval and Azure Speech audio transcription.
- Transcript persistence, display, manual edit, and consultation status progression to `Transcribed`.
- Patient- and consultation-level doctor notes.
- Structured-data display/approval surfaces when records exist.
- General/patient/consultation chat UI connected to the legacy AI client interface.
- Action confirmation and status UI connected to legacy action interface.

## Implemented but isolated

- Typed ingestion for transcripts, doctor notes, lab PDFs, and imaging PDFs.
- Deduplication, correction/supersede, continuation, retry, recovery, un-ingestion, and erasure.
- PostgreSQL/pgvector chunk storage and direct patient-scoped retrieval.
- Document and rolling patient summaries.
- Lab analyte extraction with source-cell verification.
- Ingestion quality reports and SignalR status.
- OpenAI/Azure provider wiring and OpenTelemetry.
- Evidence-threshold refusal, bilingual answer selection, grounded generation, and citation-label validation.

These features exist inside `AI/` and can be exercised directly through its API, but the main backend/frontend do not call them.

## Contract contradiction: old versus new AI

The main backend client and `backend/docs/AI_MODULE_API.md` describe a Python-style service at a configurable base URL, with:

- `/v1/transcribe`
- `/v1/extract`
- `/v1/index/documents`
- `/v1/chat`
- `/v1/actions/*`
- Callback URLs into the main backend

The repository's active AI implementation is a .NET 10 service with:

- `/ingestions`
- `/documents/{id}`
- `/patients/{id}/documents`
- `/patients/{id}/summary`
- `/patients/{id}/chat/answer`
- `/patients/{id}/data`
- SignalR ingestion status

Changing the base URL is insufficient; request/response, lifecycle, status, identity, and citation contracts all differ.

## Broken/incomplete end-to-end flows

### Transcript to AI knowledge

The transcriber and backend publish `Transcript.Ready`, but no queue consumer submits the transcript to the new AI ingestion endpoint. Therefore successful transcription does not populate pgvector.

### PDF processing

Consultation PDF upload publishes the same processing queue. The transcriber intentionally stores a placeholder transcript for documents. The new AI service has real lab/imaging PDF strategies, but nothing routes uploaded PDFs to them.

### Structured consultation data

The frontend/backend can display and approve structured JSON. The legacy callback handler can persist it. No active service in this repository calls the expected legacy extraction endpoint and callback flow; the transcriber does not extract it, and the new AI service uses a different domain model.

### Patient chat

The main chat handler calls the legacy `/v1/chat` and builds shallow patient context (patient details plus consultation dates/status). It does not call the pgvector grounded-answer endpoint or expose its rich citations.

### Action execution

The application can submit, confirm, poll, and process callbacks for AI actions, but the external `/v1/actions` implementation is absent.

### AI status relay and document controls

The AI service exposes SignalR events, document lists, un-ingestion, summaries, and erasure. The main backend and frontend do not subscribe to or expose these functions.

## Documentation contradictions

- `backend/README.md` and `backend/docs/AI_MODULE_API.md` refer to a Python AI module, while the active `AI/` implementation is .NET 10 with another contract.
- `AI/docs/retrieval-and-chat-design.md` says the backend does not exist yet; the repository contains a developed backend and frontend.
- `frontend/README.md` says the patient list relies on local recent-patient storage because no endpoint exists; current frontend code calls `GET /api/patient` and the backend implements it.
- `transcriber/README.md` describes transcript-ready publication for a future LLM Function; that consumer is still absent, but the AI service now exists as HTTP rather than a queue Function.
- AI design documents sometimes describe stronger citation verification than implemented. Code validates referenced evidence labels, not semantic support for every claim.
- Test counts in PRD 0002 (~132) are stale relative to the current source inventory.

## Production blockers and risks

### P0: address before any real patient data

1. Rotate the exposed Azure Storage credential and remove tracked secrets.
2. Strengthen authentication/password policy and decide secure browser session storage.
3. Define and implement the backend-to-new-AI adapter with authorization and lifecycle propagation.
4. Calibrate retrieval thresholds and clinically evaluate citation/answer behavior.
5. Establish compliance approval, retention, backup, erasure, and provider-region controls.

### P1: required for dependable operation

1. Replace best-effort queue publication with a durable outbox/replay path.
2. Add RabbitMQ dead-letter/retry operations.
3. Add health/readiness endpoints and deployment manifests.
4. Add end-to-end and cross-service contract tests.
5. Connect correction, deletion, and erasure across main DB, Blob Storage, and AI DB.
6. Remove transcript content previews from routine audit/telemetry unless explicitly governed.

### P2: product completeness

1. Wire grounded chat citations into the frontend.
2. Expose AI ingestion status and document inventory to clinicians.
3. Decide how structured analyte trends enter answers.
4. Add conversation storage/memory in the main backend if multi-turn behavior is desired.
5. Add scanned-document/OCR support only after quality validation.
6. Add frontend and transcriber automated test suites.

## Recommended integration sequence

1. Define one canonical internal AI contract; deprecate the old `/v1/*` documentation/client.
2. Implement a backend `ClinicalKnowledgeClient` for typed ingestion, status, documents, grounded answers, un-ingestion, and erasure.
3. Consume `Transcript.Ready` through a durable worker or replace it with a backend outbox-driven HTTP submission.
4. Route doctor notes and PDFs through the same typed ingestion model.
5. Persist AI ingestion IDs/document identities in the main database so corrections/deletions are traceable.
6. Adapt the frontend chat response to structured citations and explicit refusal state.
7. Relay/poll ingestion status and expose failed/retry controls.
8. Add coordinated deletion and end-to-end tests before enabling real patient data.

## Open product questions

The code cannot decide these; product, clinical, and compliance owners must resolve them:

- Is registration open to any visitor, invitation-only, or administrator-managed?
- Is a patient record visible only to the assigned doctor or to an authorized clinic/team?
- Which generated artifacts require doctor approval before retrieval can use them?
- What is the legally required retention policy for audio, transcripts, raw AI payloads, and deletion tombstones?
- Should general chat exist at all in a clinical product, or should every clinical answer require patient scope?
- What user experience is required when the record partially supports a question?
- Which lab providers, languages, audio environments, and specialties define the acceptance test set?

Recommended default: treat the new AI service as an internal patient-scoped evidence engine; restrict clinical chat to authorized patient context; require explicit clinician verification of generated content; and keep general-purpose AI outside the clinical record workflow.

