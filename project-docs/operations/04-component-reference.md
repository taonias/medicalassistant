# Component Reference

> Historical component snapshot. Use the [canonical system map](../../docs/architecture/system-map.md) for current deployables and ownership.

## Frontend

**Path:** `frontend/`  
**Runtime:** React 19, TypeScript 6, Vite 8  
**Default URL:** `http://localhost:4200`

Key libraries are React Router, TanStack Query, Zustand, React Hook Form, Zod, and Chart.js.

Internal areas:

- `src/app`: providers, router, and route adapters.
- `src/layouts`: authenticated shell, navigation, record action, and user menu.
- `src/features/auth`: login and persisted browser session.
- `src/features/patients`: directory, create/edit, overview, history, search, and notes.
- `src/features/record` and `audio-capture`: media recording, preview, patient attachment, and upload.
- `src/features/consultations`: consultation API, polling, detail page, files, and status.
- `src/features/transcripts`: transcript viewer and editing.
- `src/features/medical-data`: parsing/rendering structured data and approval.
- `src/features/chat`: general/patient chat plus action confirmation and status polling.
- `src/features/dashboard`: KPIs and charts.
- `src/features/settings` and `theme`: profile, password, and appearance.
- `src/shared`: HTTP client, API types, reusable controls, hooks, and formatting.

State is intentionally split: TanStack Query caches server data; Zustand persists auth, theme, and recording session state. The JWT is stored in browser local storage through Zustand persistence.

## Main backend

**Path:** `backend/`  
**Runtime:** ASP.NET Core .NET 8  
**Default URLs:** `https://localhost:7037`, `http://localhost:5109`

### `MedicalAssistant.Api`

The HTTP host. It exposes Swagger and controllers for auth, patients, consultations, transcripts, notes, chat, actions, and callbacks. Middleware translates exceptions, while Serilog writes request/application logs.

### `MedicalAssistant.Application`

The use-case layer. MediatR commands and queries are grouped by domain feature. FluentValidation runs through pipeline behavior, AutoMapper creates DTOs, and contract interfaces describe repositories and external services.

Notable use cases:

- Doctor-scoped patient CRUD and history.
- Idempotent consultation creation.
- Audio/PDF validation and upload.
- Draft/unassigned consultation management and analytics.
- Transcript read/edit.
- Doctor notes at patient or consultation level.
- Structured-data callback, read, and approval.
- Legacy context-building chat query.
- AI action trigger/callback/status.

### `MedicalAssistant.Domain`

The core entities and lifecycle methods. It is dependency-light and is referenced by the transcriber so both use the same consultation and transcript status vocabulary.

### `MedicalAssistant.Persistence`

EF Core database context, entity configuration, migrations, repositories, audit/error persistence, and startup migration. It supports PostgreSQL and SQL Server. Entity changes stamp creation/modification metadata using the current HTTP user where available.

### `MedicalAssistant.Identity`

ASP.NET Core Identity, user/profile service, JWT issuance/validation, identity migrations, and role initialization.

### `MedicalAssistant.Infrastructure`

Adapters for Azure Blob Storage, RabbitMQ publishers, PDF text extraction, logging, and the legacy AI HTTP client.

## Transcriber

**Path:** `transcriber/`  
**Runtime:** .NET 8 isolated Azure Functions  
**Trigger:** RabbitMQ `consultation.processing`

Internal areas:

- `Functions`: RabbitMQ-triggered function entry point.
- `Services`: blob retrieval, Azure Speech REST transcription, audit trail, transcript persistence orchestration, and transcript-ready publishing.
- `Persistence`: a narrow EF Core view of the shared main database.
- `Models` and `Options`: queue contracts and configuration.

The function timeout is ten minutes and RabbitMQ prefetch is one. Application Insights is configured. Audio transcription uses Azure Speech's fast transcription REST API; optional phrases can bias recognition.

For a PDF message, the transcriber does not parse the PDF. It stores a placeholder transcript explaining that speech transcription applies only to audio.

## AI ingestion, retrieval, and grounded chat

**Path:** `clinical-knowledge/`  
**Runtime:** ASP.NET Core .NET 10  
**Default URLs:** `https://localhost:7095`, `http://localhost:5293`

### Intake and strategy routing

`POST /ingestions` validates a discriminated request and persists an ingestion record before queueing work. A deterministic registry maps `SessionTranscript`, `DoctorNote`, `LabReport`, or `ImagingReport` to its strategy.

### Ingestion workers

An in-process channel feeds configurable background workers. PostgreSQL advisory locks coordinate claims and schema migration. A periodic recovery sweep finds abandoned non-terminal work.

### Prose pipeline

Session transcripts and doctor notes use model-proposed line boundaries. Code reconstructs source text verbatim, validates complete/non-overlapping coverage, applies size guardrails, embeds chunks, and commits results atomically. Model-written blurbs and summaries are stored separately from source text.

### PDF strategies

Lab and imaging reports use an `IDocumentExtractor`, backed by Azure Document Intelligence when configured. Lab panels are rendered deterministically; an agent maps analyte columns, after which code copies and verifies the source values. Imaging report text uses the prose pipeline and retains an image reference.

### Retrieval

An ordered pipeline performs scope, optional query refinement, embedding, pgvector search, and confidence-threshold packaging. Patient ID is applied in the database query as the hard boundary.

### Grounded answering

The answer service refuses deterministically when no evidence clears the threshold. Otherwise it builds an evidence-labelled prompt, generates a complete answer, and checks that every cited `[E#]` label was supplied before returning citations.

Important precision: current code verifies citation-label integrity. It does not perform automated semantic entailment checking for every sentence, and it does not reject an answer that contains no citation labels. The stronger wording in some design documents should be read as a safety objective, not a proven current guarantee.

### Providers and operations

Chat/embeddings can use OpenAI or Azure OpenAI. PDF extraction can use Azure Document Intelligence. When provider credentials are absent, the service starts with explicit unconfigured implementations that fail when the corresponding feature is invoked. OpenTelemetry export is enabled by `OTEL_EXPORTER_OTLP_ENDPOINT`.

## RabbitMQ package

**Path:** `rabbitmq/`

Docker Compose starts RabbitMQ 3.13 with the management plugin, persistent volume, health check, AMQP port 5672, and management port 15672. Root `start-rabbitmq.ps1` creates a local `.env` from the example and runs Compose.

## Tests and documentation

- `backend/test`: 2 source files with 9 `[Fact]` tests; one persistence file is still a placeholder.
- `clinical-knowledge/tests`: 53 C# source files with 166 `[Fact]` and 2 `[Theory]` declarations, using xUnit, WebApplicationFactory, Testcontainers, fake model providers, and real pgvector behavior.
- No frontend or transcriber test files were found.
- `clinical-knowledge/docs`: the most detailed design area, containing two PRDs, two design records, twelve ADRs, task spreadsheets, and the original AI glossary.

Counts are a static source inventory, not a statement that the tests passed during this documentation review.

