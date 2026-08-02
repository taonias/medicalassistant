# Medical Assistant Project Summary

This repository implements a doctor-facing medical assistant as several cooperating applications. The active project is split into five top-level components: `frontend`, `backend`, `transcriber`, `AI`, and `rabbitmq`.

## System at a glance

1. The React frontend provides the doctor's browser interface.
2. The main .NET backend owns authentication, patients, consultations, files, transcripts, structured medical data, and the public application API.
3. RabbitMQ carries consultation-processing events between services.
4. The Azure Functions transcriber retrieves uploaded files, transcribes audio with Azure Speech, updates the main database, and publishes transcript-ready events.
5. The separate AI service ingests clinical documents into PostgreSQL/pgvector and answers patient-scoped questions from retrieved evidence.

## 1. Frontend (`frontend/`)

A React 19 and TypeScript single-page application built with Vite. It uses React Router for navigation, TanStack Query for server state, Zustand for local session/recording/theme state, React Hook Form and Zod for forms, and Chart.js for dashboard visualizations.

Main feature areas:

- Authentication and protected routes.
- Dashboard and consultation analytics.
- Patient directory, patient details, history, and overview charts.
- New consultation creation through audio recording or file upload.
- Consultation status, transcript, doctor-note, and structured-data views.
- General and patient-scoped chat, including confirmation before proposed actions.
- Settings and light/dark theme support.

The development server runs on port 4200 and calls the main backend through `VITE_API_BASE_URL`. Consultation processing is polled while asynchronous work is in progress. The README notes that recent patients are also tracked locally because the original backend lacked paginated patient search; the current backend now exposes `GET /api/patient`, so that behavior should be rechecked.

## 2. Main backend (`backend/`)

A .NET 8 ASP.NET Core API organized as a Clean Architecture/CQRS solution:

- `MedicalAssistant.Domain`: core entities and enums, including patients, consultations, transcripts, doctor notes, structured medical data, action requests, audit logs, and error logs.
- `MedicalAssistant.Application`: use cases grouped by patient, consultation, transcript, doctor-note, structured-data, chat, and action-request features. It uses MediatR, FluentValidation, and AutoMapper.
- `MedicalAssistant.Persistence`: EF Core database contexts, mappings, migrations, and repositories. It supports PostgreSQL and SQL Server by configuration.
- `MedicalAssistant.Identity`: ASP.NET Core Identity, JWT authentication, user/profile services, and identity migrations.
- `MedicalAssistant.Infrastructure`: adapters for Azure Blob Storage, RabbitMQ, PDF text extraction, logging, and the AI HTTP client.
- `MedicalAssistant.Api`: controllers, middleware, dependency composition, CORS, Swagger, authentication, and authorization.

The public API covers authentication/profile management, patient records and history, consultation lifecycle and analytics, audio/PDF upload and download, transcripts, doctor notes, structured-data approval, chat, action triggering/status, and AI callbacks.

The backend stores uploaded media in Azure Blob Storage, persists operational data in the configured relational database, and publishes `consultation.processing` messages to RabbitMQ. It also contains publishers for `consultation.transcript` events and a legacy HTTP client contract for an AI module.

Tests currently include application validator tests and a persistence integration-test project; the persistence project still contains only a placeholder `UnitTest1` test.

## 3. Transcriber (`transcriber/`)

A .NET 8 isolated Azure Functions application triggered by RabbitMQ.

For each message on `consultation.processing`, it:

- Parses and audits the processing request.
- Downloads the consultation audio or document from Azure Blob Storage.
- Sends audio to Azure Speech fast transcription.
- Writes the transcript and consultation status to the main PostgreSQL database.
- Publishes a `consultation.transcript` event after the transcript is available.
- Acknowledges the inbound message only after successful completion; failures are thrown so RabbitMQ can retry them.

The function shares the main backend's `MedicalAssistant.Domain` project and includes repository, blob-retrieval, speech, audit, transcript, and RabbitMQ publisher abstractions. Application Insights provides deployed telemetry.

## 4. AI ingestion, retrieval, and grounded chat (`AI/`)

A separate .NET 10 ASP.NET Core service for patient-scoped clinical document ingestion and retrieval-augmented generation (RAG). Despite the `AI` folder name, this implementation is not Python.

Ingestion capabilities:

- Accepts session transcripts, doctor notes, laboratory reports, and imaging reports.
- Routes each declared document type through a deterministic ingestion strategy.
- Extracts PDF text/tables through Azure Document Intelligence when configured.
- Creates semantic chunks, summaries, embeddings, and structured analyte results.
- Handles duplicate submissions, corrections, retries, crash recovery, document un-ingestion, and full patient erasure.
- Processes submissions asynchronously with background workers and exposes progress through polling and a SignalR hub.

Retrieval and chat capabilities:

- Enforces the patient scope before retrieval.
- Optionally refines questions, embeds them, performs pgvector similarity search, and packages evidence with provenance.
- Generates answers only from supplied evidence, verifies citations, and returns an explicit insufficient-evidence response when grounding is inadequate.
- Exposes patient document/summary operations and `POST /patients/{patientId}/chat/answer`.

Storage, security, and operations:

- Uses a dedicated PostgreSQL database with the pgvector extension for ingestion state, document chunks, embeddings, summaries, analyte results, and agent instructions.
- Owns its schema through EF Core migrations and applies migrations under a PostgreSQL advisory lock.
- Uses API-key authentication by default and a separate admin authorization requirement for patient erasure.
- Supports OpenAI or Azure OpenAI providers, OpenTelemetry export, an optional local document archive, and Swagger.
- Has a substantial integration-test suite (46 test source files) using xUnit and Testcontainers PostgreSQL/pgvector.

The `AI/docs` directory contains PRDs, architecture decision records, design documents, and task spreadsheets. `AI/docs/helping_documents_out_of_repo` is reference/archive material from another project and is not part of the active runtime described here.

## 5. RabbitMQ infrastructure (`rabbitmq/`)

A Docker Compose definition for RabbitMQ 3.13 with the management plugin. It exposes AMQP on port 5672 and the management UI on port 15672, persists broker data in a Docker volume, and includes a health check.

The root `start-rabbitmq.ps1` script validates Docker, creates `rabbitmq/.env` from the example when necessary, and starts the broker. The two named workflow queues are:

- `consultation.processing`: main backend to transcriber.
- `consultation.transcript`: transcriber/backend transcript-ready publishers to a downstream consumer.

## End-to-end flows

### Consultation recording and transcription

`Frontend -> Main backend -> Azure Blob Storage + main database -> consultation.processing queue -> Transcriber -> Azure Speech -> main database -> consultation.transcript queue`

### Clinical document ingestion and grounded chat

`Main backend -> AI HTTP API -> ingestion worker -> PostgreSQL/pgvector -> retrieval pipeline -> grounded answer with verified citations -> main backend -> frontend`

## External dependencies

- PostgreSQL for the main application and a separate PostgreSQL/pgvector store for the AI service.
- Azure Blob Storage for consultation files.
- Azure Speech for transcription.
- OpenAI or Azure OpenAI for chat and embeddings.
- Azure Document Intelligence for PDF extraction.
- RabbitMQ for asynchronous consultation/transcript events.
- Docker Desktop for local RabbitMQ and AI database/test containers.

## Current integration and maintenance notes

- The main backend's `AiModuleHttpClient` and `backend/docs/AI_MODULE_API.md` describe a legacy `/v1/transcribe`, `/v1/extract`, `/v1/chat`, and `/v1/actions` contract for a "Python AI Module." The active .NET 10 AI service exposes `/ingestions`, `/documents`, `/patients/...`, and grounded-chat endpoints instead. These contracts are not directly compatible and need an adapter or backend client update.
- Both the backend and transcriber can publish `consultation.transcript`, but no active consumer for that queue was found in this repository. The AI service currently accepts ingestion through HTTP.
- The main backend targets .NET 8 while the AI service targets .NET 10, so local and deployment environments need both SDK/runtime generations.
- `backend/src/MedicalAssistant.Api/appsettings.json` contains credentials and a live-looking Azure Storage account key in source control. Rotate exposed secrets and move them to user secrets, environment variables, or a managed secret store.
- Root-level orchestration starts RabbitMQ only; there is no single compose or startup definition for the frontend, backend, databases, transcriber, and AI service together.

