# Medical Assistant — Clinical Knowledge

.NET 10 service that ingests clinical documents about a patient into a vector store and answers a doctor's questions about that patient, grounded only in evidence it can cite. Receives documents via authenticated HTTP from the main backend — the browser never talks to this service directly.

See [CONTEXT.md](CONTEXT.md) for the full domain glossary and [docs/adr/](docs/adr/) for the twelve accepted architecture decisions behind it.

## Structure

```
clinical-knowledge/
├── src/
│   └── MedicalAssistance.Ingestion.Api/
│       ├── Modules/
│       │   ├── Ingestion/          # Document intake, processing, persistence
│       │   ├── DocumentLifecycle/  # Patient documents/summary, un-ingest
│       │   ├── GroundedChat/       # Retrieval + grounded answers
│       │   └── Retrieval/
│       ├── Adapters/
│       ├── BuildingBlocks/
│       └── Security/                # API-key authentication
├── tests/
│   └── MedicalAssistance.Ingestion.Api.Tests/
├── database/                        # pgvector container + migration workflow (see database/README.md)
├── docs/
│   ├── adr/                         # This service's own 12 ADRs
│   ├── ingestion-pipeline-design.md
│   ├── retrieval-and-chat-design.md
│   └── prd/
└── MedicalAssistance.Ingestion.slnx
```

## Prerequisites

- .NET 10 SDK
- Docker Desktop, for the `pgvector/pgvector:pg17` container and the Testcontainers-backed integration test suite
- An OpenAI (or Azure OpenAI) API key for chat and embeddings
- Azure Document Intelligence, for lab/imaging PDF extraction

## Run

```powershell
cd AI
dotnet run --project src/MedicalAssistance.Ingestion.Api
```

- `https://localhost:7095/swagger`
- `http://localhost:5293/swagger`

(Ports from `src/MedicalAssistance.Ingestion.Api/Properties/launchSettings.json`.)

The service authenticates every request with an API key and refuses to start without at least one configured — see `Authentication:ApiKeys` below. It can boot with model/extraction providers unconfigured, but ingestion and chat paths that need them fail explicitly when invoked rather than silently.

## Key endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/ingestions` | Submit a Document for ingestion |
| GET | `/ingestions` | List a doctor's ingestions |
| GET | `/ingestions/{id}` | Ingestion status |
| GET | `/ingestions/{id}/quality` | Chunking quality report |
| POST | `/ingestions/{id}/retry` | Retry a failed ingestion |
| GET | `/patients/{patientId}/documents` | List a patient's documents |
| GET | `/patients/{patientId}/summary` | Patient Summary (rolling overview) |
| DELETE | `/patients/{patientId}/data` | GDPR Erasure |
| DELETE | `/documents/{documentId}` | Un-ingest one document |
| POST | `/patients/{patientId}/chat/answer` | Grounded chat answer |
| POST | `/patients/{patientId}/chat/summarize` | Conversation summarization |

## Configuration

`appsettings.Development.json` (tracked, non-secret) sets model names and the local connection string (`pgvector` on port 5433, database `ai_med`). Secrets — `Authentication:ApiKeys`, `OpenAIChat:ApiKey`, `OpenAIEmbeddings:ApiKey`, Document Intelligence keys — come from `dotnet user-secrets` or an environment/secret store, never a tracked file. See [database/README.md](database/README.md) for the database container and its destructive-reset warning before running `createDB.ps1`.
