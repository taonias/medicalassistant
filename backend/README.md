# Medical Assistant Backend

.NET 8 CQRS + Clean Architecture backend for the medical assistant system.

## Structure

```
backend/
├── src/
│   ├── MedicalAssistant.Domain/
│   ├── MedicalAssistant.Application/
│   ├── MedicalAssistant.Persistence/
│   ├── MedicalAssistant.Infrastructure/
│   ├── MedicalAssistant.Identity/
│   ├── MedicalAssistant.Api/
│   ├── MedicalAssistant.Transcription.Worker/     # standalone worker (ADR-0001) — see docs/modules/consultation-processing.md
│   ├── MedicalAssistant.EventBus/                 # transport abstractions — see docs/modules/durable-messaging.md
│   ├── MedicalAssistant.EventBusRabbitMQ/          # RabbitMQ adapter
│   ├── MedicalAssistant.ConsultationProcessing.Contracts/  # versioned integration-event contracts (ADR-0004)
│   └── MedicalAssistant.Migrations/                # one-shot schema-migration job
├── test/
│   └── MedicalAssistant.Architecture.Tests/        # layering enforcement (R38)
├── docs/
│   └── AI_MODULE_API.md      # the legacy /v1/* HTTP/callback contract — see below
└── MedicalAssistant.slnx
```

See [docs/modules/](../docs/modules/) for a README per ownership area (Consultation Lifecycle, Clinical Record, Identity & Preferences, etc.) and the root [CONTEXT-MAP.md](../CONTEXT-MAP.md) for the full bounded-context picture — this file covers only what's needed to get the backend running.

## Prerequisites

- .NET 8 SDK
- PostgreSQL (or SQL Server via `Database:Provider`)
- Azure Blob Storage (or Azurite with `UseDevelopmentStorage=true`)
- RabbitMQ, for the event-bus paths (see `docker-compose.yml` at the repo root)
- The Clinical Knowledge service (`clinical-knowledge/`, .NET 10 — **not** Python, despite what an older version of this file said) for chat/ingestion; see [clinical-knowledge/README.md](../clinical-knowledge/README.md). `docs/AI_MODULE_API.md` documents a distinct, legacy `/v1/*` contract with no matching current implementation (`Modules/Integrations/LegacyAiModule/`) — the real integration is `Modules/Integrations/ClinicalKnowledge/`.

## Run

```bash
cd backend
dotnet ef database update --project src/MedicalAssistant.Persistence --startup-project src/MedicalAssistant.Api
dotnet run --project src/MedicalAssistant.Api
```

Swagger: `https://localhost:7037/swagger` (port confirmed against `launchSettings.json`, `docker-compose.yml`, and the root `HOW-TO-RUN.md` — all three agree).

## Key endpoints

A representative sample, not exhaustive — see each module's own README under [docs/modules/](../docs/modules/) for its full surface.

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/auth/register` | Register doctor |
| POST | `/api/auth/login` | JWT login |
| POST | `/api/patient` | Create patient |
| GET | `/api/patient/{id}/history` | Patient history |
| POST | `/api/consultation` | Create consultation |
| POST | `/api/consultation/{id}/audio` | Upload audio |
| POST | `/api/chat/query` | Chat with AI context |
| POST | `/api/action/trigger` | Trigger AI action |
| POST | `/api/ai-callback/*` | Legacy AI module webhooks (see the note above) |

## Configuration

Edit `src/MedicalAssistant.Api/appsettings.json` for connection strings, JWT, Blob Storage, and AI module URLs.
