# Local Operations Guide

This guide describes the repository as it exists. A fully integrated one-command environment is not available.

## Prerequisites

- .NET 8 SDK/runtime for the main backend and transcriber.
- .NET 10 SDK/runtime for the AI service.
- Node.js/npm compatible with Vite 8 and TypeScript 6.
- Docker Desktop for RabbitMQ, AI PostgreSQL/pgvector, and AI integration tests.
- PostgreSQL on port 5432 for the main application, unless SQL Server is selected.
- Azure Functions Core Tools v4 for `func start` (optional when using `dotnet run`).
- Azurite or a real `AzureWebJobsStorage` for the Functions host.
- Azure Blob Storage reachable by both backend and transcriber.
- Azure Speech for real audio transcription.
- OpenAI or Azure OpenAI for AI chat/embedding behavior.
- Azure Document Intelligence for lab/imaging PDF extraction.

## Secret handling before first run

Do not use tracked production credentials. Configure secrets using environment variables, .NET user secrets, Azure App Settings, or an approved secret store.

The tracked main-backend `appsettings.json` contains a live-looking Blob Storage account key and development credentials. Treat it as exposed: rotate it before using the account and remove it from normal configuration history in a separate security change.

## 1. Start the main database

The default main connection is PostgreSQL on `localhost:5432`, database `MedicalAssistantDb`. The repository does not provide a main-database Compose file.

The backend applies persistence migrations at startup and seeds Identity roles. Confirm the configured database account has migration permissions. The application and Identity contexts use the selected provider independently but resolve the same provider/connection configuration.

## 2. Start RabbitMQ

From the repository root:

```powershell
.\start-rabbitmq.ps1
```

The script creates `rabbitmq/.env` from `.env.example` when missing and runs `docker compose up -d`. Verify:

- AMQP: `localhost:5672`
- Management UI: `http://localhost:15672`
- Container health: `medicalassistant-rabbitmq`

Align RabbitMQ usernames/passwords across the Compose environment, backend `RabbitMq` configuration, and the transcriber's `RabbitMqConnection` URI.

## 3. Configure Blob Storage

The backend uploads to `BlobStorage:ConsultationAudioContainer` and `BlobStorage:ConsultationDocumentsContainer`. The transcriber must use the same account and container names so it can resolve the published blob URI.

For local Azurite, use `UseDevelopmentStorage=true` consistently where supported. The Functions host also needs `AzureWebJobsStorage`, independently of consultation-file storage.

## 4. Run the main backend

```powershell
cd backend
dotnet run --project src/MedicalAssistant.Api
```

Expected development endpoints:

- API/Swagger: `https://localhost:7037/swagger`
- HTTP fallback: `http://localhost:5109`

Required configuration groups include `Database`, `ConnectionStrings`, `JwtSettings`, `BlobStorage`, `RabbitMq`, `AiModule`, `AiCallback`, `AppSettings`, and `CorsSettings`.

The backend can run without the new AI service, but chat, note indexing, structured extraction, and AI actions will not work against it without the missing adapter.

## 5. Run the transcriber

Create the local settings file without committing it:

```powershell
cd transcriber
Copy-Item local.settings.json.example local.settings.json
dotnet run
```

Or use:

```powershell
func start
```

Configure:

- `RabbitMqConnection`
- `RabbitMqQueueName=consultation.processing`
- `RabbitMqConsultationTranscriptQueueName=consultation.transcript`
- Blob Storage connection/container keys
- Azure Speech key, region, locale, and optional phrase list
- Main PostgreSQL connection string
- `AzureWebJobsStorage`

For Greek recognition, the example uses `el-GR`. That is speech recognition, not translation.

## 6. Start the AI database

The AI service uses `pgvector/pgvector:pg17` on host port 5433, database `ai_med`, in the documented development setup.

The supplied `AI/database/createDB.ps1` drops and recreates the database before applying migrations. It is suitable only for disposable local data. Do not use the reset path on a database containing records that must be retained.

The AI host also calls EF Core migrations at startup under an advisory lock.

## 7. Configure and run the AI service

Development model names are already in `appsettings.Development.json`. Add provider keys with user secrets or environment variables. At minimum:

- `ConnectionStrings:Postgres`
- `Authentication:ApiKeys` (the service refuses to start without a standard key)
- Optional separate admin API key for erasure
- `OpenAIChat:ApiKey` and `OpenAIEmbeddings:ApiKey`, or Azure equivalents
- Azure Document Intelligence configuration for PDF ingestion
- Optional `OTEL_EXPORTER_OTLP_ENDPOINT`

Then:

```powershell
cd AI
dotnet run --project src/MedicalAssistance.Ingestion.Api
```

Expected endpoints:

- `https://localhost:7095/swagger`
- `http://localhost:5293/swagger`

The service can boot with unconfigured model/extraction implementations, but ingestion/chat paths requiring them fail explicitly when invoked.

## 8. Run the frontend

```powershell
cd frontend
npm install
npm run dev
```

The app runs at `http://localhost:4200`. Copy `.env.example` to `.env` if the backend base URL differs.

## Suggested startup order

1. Main database and Blob/Azurite.
2. RabbitMQ.
3. AI PostgreSQL/pgvector, if testing the AI service separately.
4. Main backend.
5. AI service, if testing its API separately.
6. Transcriber.
7. Frontend.

## Health checks and smoke checks

No dedicated health endpoints were found. Use these checks:

- RabbitMQ container health and management UI.
- Main backend Swagger loads and login succeeds.
- Create a patient and consultation through Swagger/UI.
- Upload a short test audio and confirm `consultation.processing` drains.
- Confirm an audit row, transcript row, and `Transcribed` consultation state.
- AI Swagger loads with a valid API key and migrations complete.
- Submit a known small transcript to `/ingestions`, poll status, then list patient documents.
- Ask an answerable and an unanswerable question directly against the AI chat endpoint.

## Troubleshooting

| Symptom | Likely area | Checks |
| --- | --- | --- |
| Upload succeeds but never processes | Best-effort RabbitMQ publish | Backend warning log, queue depth, broker credentials; re-upload is not automatically replayed |
| Message keeps returning | Transcriber/provider/database failure | Function log, AuditLogs failure entry, Speech/Blob/database config |
| Audio transcription fails immediately | Azure Speech config/content | Key, region, locale, MIME type, file bytes, ten-minute timeout |
| PDF yields only explanatory text | Expected current transcriber behavior | Use the AI PDF ingestion API separately; no PDF extractor is wired to consultation upload |
| Frontend logs out | API returned 401 | JWT lifetime, issuer/audience/key, browser persisted token |
| Chat returns 404/provider error | AI contract mismatch | Main backend calls legacy `/v1/*`; new AI API is not compatible |
| AI service fails at startup | Required auth/database/vector config | API key array, PostgreSQL reachability, migrations, embedding dimension |
| AI ingestion fails on model call | Provider not configured | OpenAI/Azure keys, model/deployment names, regional availability |
| AI retrieval returns weak matches | Threshold/calibration | Current default confidence threshold is permissive (`0.0`); calibrate against a clinical golden set |

## Production-operation gaps

Before production, add environment-specific deployment manifests, health/readiness probes, secret-store integration, backup/restore procedures, retention schedules, queue dead-letter/replay operations, incident runbooks, and compatibility tests between services.

