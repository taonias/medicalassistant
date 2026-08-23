# Medical Assistant System Map

Status: canonical refactoring baseline

Verified against source at commit: ed2e38e

Baseline date: 23 August 2026

## How to resolve conflicting documentation

Use this order of authority:

1. Executable source, database migrations, and tests.
2. Docker Compose manifests and environment templates.
3. The root context map, this system map, and the observable interface baseline.
4. Accepted ADRs in each bounded context.
5. Dated reviews under project-docs.
6. Generated graph output.

Older reviews remain useful evidence, but they do not override the running code.

## Product purpose

The product supports a Doctor through a Consultation: manage Patients, capture a Recording or Consultation Document, obtain a Transcript, derive Clinical Knowledge, converse over Patient material, and review or approve Structured Medical Data. Authentication and Consultation workflow stay in the core backend. Speech transcription and Clinical Knowledge are separate processing responsibilities connected through durable Integration Events.

## Bounded contexts

| Context | Owns | Does not own |
|---|---|---|
| Care Workflow | Doctor identity, Patients, Consultations, files, Transcripts, Doctor Notes, conversation records, Doctor-facing workflow | Speech-provider execution or clinical retrieval/indexing |
| Consultation Processing | Retrieving a Recording, speech-provider selection, transcription, durable publication of Transcript outcomes | Patient Record workflow or Transcript text distribution through RabbitMQ |
| Clinical Knowledge | Clinical Knowledge Document ingestion, chunking, embeddings, retrieval, derived Patient Summary records (current code term), grounded chat | Authentication, Consultation lifecycle, or source Recording |

The detailed context relationships and language live in the root [context map](../../CONTEXT-MAP.md).

## Deployable units

| Deployable | Technology | Primary responsibility | Main dependencies |
|---|---|---|---|
| Frontend | React/Vite | Doctor UI and browser workflow | Backend HTTP API and backend SignalR hub |
| Backend API | .NET 8 | Care Workflow HTTP API, event publication/subscription, callback compatibility | PostgreSQL, blob storage, RabbitMQ, Clinical Knowledge |
| Backend Migrations | .NET 8 | Applies Care Workflow schemas before API startup | PostgreSQL |
| Transcription Worker | .NET 8 | Consumes Consultation Audio Uploaded and produces Transcript Ready or Transcription Failed | RabbitMQ, blob storage, speech provider, PostgreSQL |
| Clinical Knowledge | .NET 10 | Ingestion, retrieval, grounded chat, derived Patient Summary records (current code term) | PostgreSQL/pgvector, RabbitMQ, OpenAI/Azure services |
| Production edge | Nginx | Routes browser traffic in production | Frontend and backend services |

## Runtime topology

\`\`\`mermaid
flowchart LR
    Browser[Doctor browser] --> Frontend[React frontend]
    Frontend --> API[Care Workflow API]
    API --> CareDb[(Care Workflow PostgreSQL)]
    API --> Blob[(Blob storage)]
    API -->|outbox| Rabbit[(RabbitMQ)]
    Rabbit --> Worker[Transcription Worker]
    Worker --> Blob
    Worker -->|inbox + transcript + outbox| CareDb
    Worker --> Rabbit
    Rabbit --> API
    API -->|HTTP ingestion/chat| CK[Clinical Knowledge]
    CK --> KnowledgeDb[(Clinical Knowledge PostgreSQL + pgvector)]
    CK -->|outbox events| Rabbit
\`\`\`

RabbitMQ carries identifiers and state-change facts, not transcript text. Consumers retrieve authoritative data from the owning context.

## Critical flows

### Recording to Transcript

1. The frontend uploads a Recording to the Backend API.
2. The backend stores the Recording blob, updates file state, and writes a Consultation Audio Uploaded event to its outbox in the same database transaction.
3. The API-hosted outbox relay publishes the event to RabbitMQ.
4. The Transcription Worker consumes it, retrieves the Recording, and invokes the configured speech provider.
5. The worker atomically records its inbox receipt, transcript/status change, and a Transcript Ready or Transcription Failed outbox event.
6. The worker relay publishes the outcome.
7. The Backend API consumes the outcome for workflow notifications and downstream orchestration.

### Transcript to clinical knowledge

On Transcript Ready, the backend subscriber reads authoritative transcript data and calls the Clinical Knowledge ingestion API. The transcript body does not travel in the event envelope. Clinical Knowledge persists ingestion state and processes it asynchronously.

### Grounded chat

The frontend asks the Backend API. The backend enforces the Doctor/Patient boundary, delegates retrieval and answer generation to Clinical Knowledge, and persists the conversation record it owns.

### Consultation deletion

Care Workflow is the deletion authority. It tombstones the Consultation and signals downstream cleanup. Cross-service deletion remains eventually consistent and is listed in the risk baseline.

### Document upload

The backend accepts and stores a Consultation Document and can publish Consultation Document Uploaded. No document-processing consumer currently completes this path, so a Consultation Document can remain pending. This is current behavior, not a refactoring target in Wave 0.

## Data ownership

| Data | System of record | Other copies |
|---|---|---|
| Doctors and Doctor profiles | Care Workflow | Browser session state only |
| Patients and consultations | Care Workflow | Clinical Knowledge receives patient-scoped identifiers |
| Recordings and Consultation Documents | Care Workflow blob storage plus file metadata | Worker retrieves a Recording temporarily |
| Transcript text and revision | Care Workflow | Clinical Knowledge stores derived chunks/embeddings |
| Event delivery state | Each producer's outbox and each consumer's inbox | RabbitMQ is transport, not system of record |
| Embeddings, chunks, ingestion records, summaries | Clinical Knowledge | None authoritative elsewhere |
| Conversation records | Care Workflow | Clinical Knowledge handles generation context |

## Hosting facts that refactors must preserve

- The Backend API hosts its outbox relay, the RabbitMQ outcome consumer, and conversation-summary processing.
- The Transcription Worker is a standalone generic-host process. It registers a readiness health check, but the current host does not expose an HTTP health endpoint.
- Clinical Knowledge hosts ingestion, recovery, and integration-event relay workers.
- Root Compose is the local cross-service orchestration contract.
- Production deployment reads deploy/.env.prod; deploy/.env.prod.example is the template copied to create it. The root .env.example is not the production contract.
- Health endpoints and startup ordering are operational interfaces, not incidental implementation details.

## Ownership seams for the team

Assign ownership vertically by bounded context and capability:

- Care Workflow: Doctor identity, Patient Record, Consultation capture, conversations, and integration infrastructure.
- Consultation Processing: audio acquisition, transcription providers, transcription workflow, and its event adapters.
- Clinical Knowledge: ingestion, retrieval, chat generation, derived Patient Summary records (current code term), and its event adapters.
- Platform/experience: deployment manifests, observability, release checks, and frontend shell/design system.

Within a context, prefer folders shaped as context → capability → use case. Keep HTTP, persistence, and messaging adapters at the capability boundary so a developer can understand a workflow without crossing unrelated technical-layer folders.

## Domain-modeling gap

The Clinical Knowledge code and database use PatientSummary/PatientSummaries for a derived rolling overview. No bounded-context glossary currently defines a canonical business term for that concept. This map retains the recognizable code term only to describe current behavior; R36 must resolve and document the term before module naming or ownership is finalized.

## Refactoring guardrails

- Do not change routes, event names or payloads, persistence schema, configuration keys, Compose service names, health endpoints, or browser contracts during structural moves.
- Make one capability move at a time and retain compatibility adapters where a public namespace or interface must move.
- Add characterization tests before moving behavior that is not already protected.
- Treat suspected defects as separate tickets; do not silently correct them inside a structural commit.

See the [observable interface baseline](observable-interface-baseline.md) for protected contracts and the [risk baseline](../known-issues/refactor-baseline.md) for known or suspected defects.
