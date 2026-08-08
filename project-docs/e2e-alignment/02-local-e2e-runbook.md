# Local Full-System E2E Runbook

How to run the whole Medical Assistant stack locally and drive the real-user journey. Covers the connected product: frontend (Vite), backend API, app + clinical PostgreSQL, Azurite, RabbitMQ, the standalone Transcription Worker, and the Clinical Knowledge service.

## Prerequisites

- Docker Desktop running.
- Node.js (for the Vite frontend).
- A local `.env` at the repo root, created from `compose.env.example` (gitignored). It is the single source of truth for local secrets.
- **Two external model providers are optional and gate the last two hops only:**
  - **Azure Speech** (`AZURE_SPEECH_KEY`, `AZURE_SPEECH_REGION` in `.env`) — required for real transcription.
  - **OpenAI** for Clinical Knowledge embeddings + chat — set on the `clinical-knowledge` service (e.g. `OpenAIChat__ApiKey`, `OpenAIEmbeddings__ApiKey`; embeddings model must stay `text-embedding-3-large` / 3072 dims). Without it, ingestion is accepted and recorded but fails at the embedding step.

Everything up to those two model calls runs with no external credentials.

## Ports

| Service | URL |
| --- | --- |
| Frontend (Vite) | http://localhost:5173 |
| Backend API (HTTP) | http://localhost:7037 — Swagger at `/swagger`, health at `/health/live` and `/health/ready` |
| Clinical Knowledge API | http://localhost:8000/swagger |
| RabbitMQ management | http://localhost:15672 |
| App PostgreSQL | localhost:5432 (`MedicalAssistantDb`) |
| Clinical PostgreSQL | localhost:5434 (`ai_med`, pgvector) — remapped off 5433 via `docker-compose.override.yml` |

## Start

```bash
# from repo root
docker compose up -d --build
# frontend runs outside compose:
cd frontend && npm install && npm run dev -- --host --port 5173
```

Order is handled by compose health gates: databases + rabbitmq + azurite come up, `rabbitmq-provisioner` creates least-privilege users, `backend-migrations` runs once and exits, then `backend-api`, `transcription-worker`, and `clinical-knowledge` start. `backend-migrations` and `rabbitmq-provisioner` are one-shot and should show `Exited (0)`.

## Health checks

```bash
docker compose ps
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:7037/health/ready   # 200
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8000/swagger/v1/swagger.json  # 200
```

## Real-user path (browser)

1. Open http://localhost:5173, register a doctor, log in.
2. Patients → Add new patient.
3. Open the patient → Consultations → upload a short WAV/MP3 (`.webm/.wav/.mp3/.ogg`, ≤100 MB).
4. Watch the consultation status: `Draft` → `AudioUploaded` → (worker) → `Transcribed`/`Failed`.

## API smoke (no browser)

```bash
# register
curl -s -X POST http://localhost:7037/api/Auth/register -H "content-type: application/json" \
  -d '{"email":"dr@example.com","userName":"drsmith","password":"Passw0rd!23","firstName":"Alice","lastName":"Smith"}'
# login -> capture .token
curl -s -X POST http://localhost:7037/api/Auth/login -H "content-type: application/json" \
  -d '{"userName":"drsmith","password":"Passw0rd!23"}'
# with $TOKEN:
curl -s -X POST http://localhost:7037/api/Patient -H "Authorization: Bearer $TOKEN" -H "content-type: application/json" \
  -d '{"firstName":"John","lastName":"Doe","dateOfBirth":"1980-05-15"}'
curl -s -X POST http://localhost:7037/api/Consultation -H "Authorization: Bearer $TOKEN" -H "content-type: application/json" \
  -d '{"patientId":1}'
curl -s -X POST http://localhost:7037/api/Consultation/1/audio -H "Authorization: Bearer $TOKEN" \
  -F "audioFile=@sample.wav;type=audio/wav" -F "durationSeconds=5"
curl -s http://localhost:7037/api/Consultation/1 -H "Authorization: Bearer $TOKEN"
```

## Inspection

```bash
# outbox published?
docker compose exec -T postgres-app psql -U medicalassistant -d MedicalAssistantDb \
  -c 'SELECT "EventType","Status","PublishedAtUtc" IS NOT NULL AS published FROM "ConsultationOutboxMessages" ORDER BY "OccurredAtUtc" DESC LIMIT 5;'
# rabbitmq queues (consumers should be 1 on the two main queues)
curl -s -u "$(grep ^RABBITMQ_BOOTSTRAP_USER= .env|cut -d= -f2):$(grep ^RABBITMQ_BOOTSTRAP_PASSWORD= .env|cut -d= -f2)" \
  http://localhost:15672/api/queues/%2F
# clinical ingestion status
curl -s http://localhost:8000/ingestions/<id> -H "X-Api-Key: local-development-key"
```

## Credential-gated steps

- **Transcription (Azure Speech):** set `AZURE_SPEECH_KEY`/`AZURE_SPEECH_REGION` in `.env`, then `docker compose up -d transcription-worker`. Without them the worker classifies the failure as `speech-key-missing` (consultation → `Failed`) — no crash, no secret leak.
- **Clinical Knowledge (OpenAI):** set the OpenAI section on `clinical-knowledge`, then `docker compose up -d clinical-knowledge`. Without it, `POST /ingestions` returns `202` + `ingestionId` but the ingestion ends `Failed` with "No chat provider is configured".

## Shutdown

```bash
docker compose stop            # pause, keep state
docker compose start           # resume
docker compose down            # remove containers, keep volumes
docker compose down --volumes  # full reset (re-migrates from empty)
```

## Notes / known follow-ups

- Azurite is reached with an emulator-mode connection string (`UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://azurite`) so shared-key signing works behind the docker service name; a plain `BlobEndpoint=...` connection string fails with `AuthorizationFailure`.
- DB-level retry (`EnableRetryOnFailure`) is disabled on the app context because the durable paths use explicit transactions; RabbitMQ retry queues provide resilience. Follow-up: wrap those transactions in EF execution strategies and re-enable retry.
- Multi-event-type RabbitMQ retry re-routing is a known limitation (single dead-letter routing key); follow-up tracked.
- `consultation.transcription-failed.v1` has no bound queue, so the relay's mandatory publish of it is unroutable (failure path only).
