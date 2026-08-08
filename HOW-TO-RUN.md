# How to Run

## Prerequisites
- Docker Desktop running
- Node.js (for the frontend)
- A root `.env` copied from `compose.env.example` (fill in keys below)

## Start everything
```bash
# from repo root — brings up all backend services
docker compose up -d --build

# frontend runs separately
cd frontend && npm install && npm run dev -- --host --port 5173
```
Open **http://localhost:5173**, register a doctor, and go.

## Access & endpoints

| Component | URL / endpoint | Auth |
|---|---|---|
| Frontend (app) | http://localhost:5173 | log in as a doctor |
| Backend API | http://localhost:7037 | JWT (from `/api/Auth/login`) |
| Backend API docs | http://localhost:7037/swagger | — |
| Backend health | http://localhost:7037/health/ready | — |
| Clinical Knowledge API | http://localhost:8000 | header `X-Api-Key` |
| Clinical Knowledge docs | http://localhost:8000/swagger | — |
| RabbitMQ management UI | http://localhost:15672 | broker user/pass |
| App PostgreSQL | localhost:5432 / db `MedicalAssistantDb` | user/pass |
| Clinical PostgreSQL | localhost:5434 / db `ai_med` | user/pass |
| Azurite (Blob) | http://localhost:10000/devstoreaccount1 | account key |

## Test credentials

All are **local-dev only** (values come from your `.env`).

- **App doctor login** — register any account in the UI, or use this test one:
  - username `drsmith` · password `Passw0rd!23`
  - create it via API if it doesn't exist:
    ```bash
    curl -X POST http://localhost:7037/api/Auth/register -H "content-type: application/json" \
      -d '{"email":"dr@example.com","userName":"drsmith","password":"Passw0rd!23","firstName":"Alice","lastName":"Smith"}'
    ```
- **Clinical Knowledge API** — header `X-Api-Key: local-development-key` (`CLINICAL_KNOWLEDGE_API_KEY`)
- **RabbitMQ UI** — user `medicalassistant-broker-bootstrap` / pass = `RABBITMQ_BOOTSTRAP_PASSWORD`
- **App PostgreSQL** — user `medicalassistant` / pass = `POSTGRES_APP_PASSWORD`
- **Clinical PostgreSQL** — user `clinicalknowledge` / pass = `POSTGRES_CLINICAL_PASSWORD`
- **Azurite** — account `devstoreaccount1`, well-known dev key (`Eby8vd...E4E2j+Q==`)

## Keys (in `.env`)
```
TRANSCRIPTION_PROVIDER=OpenAiWhisper     # or AzureSpeech
OPENAI_WHISPER_API_KEY=sk-...            # transcription (gpt-4o-transcribe)
OPENAI_API_KEY=sk-...                    # Clinical Knowledge chat + embeddings
```
Leave a provider's key blank to skip that step (it fails cleanly, no crash).

## Stop
```bash
docker compose stop            # pause, keep data
docker compose down --volumes  # full reset
```

## Components

| Component | Port | What it does |
|---|---|---|
| **Frontend** | 5173 | React + Vite web app. The doctor's UI: sign in, manage patients, upload consultations, view transcripts and status. |
| **Backend API** | 7037 | ASP.NET Core API. Owns auth, patients, consultations, blob upload, and the outbox that publishes events. The system's front door. |
| **Transcription Worker** | — | Standalone service. Consumes audio-uploaded events, pulls the audio, transcribes via OpenAI Whisper (or Azure Speech), stores the transcript, emits transcript-ready. |
| **Clinical Knowledge** | 8000 | AI service (`AI/`). Ingests transcripts/notes/reports into a pgvector store and answers doctor questions with grounded, cited RAG. |
| **App PostgreSQL** | 5432 | Main database: users, patients, consultations, transcripts, outbox/inbox. |
| **Clinical PostgreSQL** | 5434 | pgvector database for Clinical Knowledge: document chunks + embeddings. |
| **RabbitMQ** | 5672 / 15672 | Event bus connecting backend ↔ worker ↔ Clinical Knowledge. Management UI at :15672. |
| **Azurite** | 10000 | Local Azure Blob emulator. Stores uploaded consultation audio/PDF files. |
| **Backend Migrations** | — | One-shot job. Applies the app database schema on startup, then exits. |
| **RabbitMQ Provisioner** | — | One-shot job. Creates the least-privilege broker users and permissions, then exits. |

## Health checks
```bash
docker compose ps
curl -s http://localhost:7037/health/ready        # backend
curl -s http://localhost:8000/swagger/v1/swagger.json   # clinical knowledge
# RabbitMQ UI: http://localhost:15672
```

See `project-docs/e2e-alignment/02-local-e2e-runbook.md` for the full runbook.
