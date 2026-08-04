# Legacy Function and Direct Messaging Removal Verification

T36 closes the unused pre-production Azure Function path and legacy direct RabbitMQ messaging path. The user confirmed the Function never ran in a deployed environment, so there is no production backlog, Function execution history, or live compatibility path to migrate.

## Removed active source

- Deleted the `transcriber` Azure Function project, including its Function entry point, `host.json`, local settings example, Function-specific project file, direct result publisher, and duplicated Blob/Speech/persistence code.
- Deleted backend legacy direct RabbitMQ publisher contracts/classes:
  - `IConsultationProcessingPublisher`
  - `ITranscriptReadyPublisher`
  - `RabbitMqConsultationProcessingPublisher`
  - `RabbitMqTranscriptReadyPublisher`
- Deleted legacy direct-message DTOs and queue settings:
  - `ConsultationProcessingMessage`
  - `TranscriptReadyMessage`
  - `RabbitMqSettings`
- Removed lower-case `RabbitMq` legacy queue configuration from backend appsettings and root Compose.

## Preserved replacement behavior

Useful behavior from the old path now lives in the target architecture:

| Legacy responsibility | Replacement |
| --- | --- |
| Function RabbitMQ trigger | standalone Transcription Worker hosted service |
| direct `consultation.processing` queue publication | durable outbox `consultation.audio-uploaded.v1` / `consultation.document-uploaded.v1` |
| direct `consultation.transcript` queue publication | outbox `consultation.transcript-ready.v1` plus backend-owned consumer |
| queue-draining deletion behavior | authoritative deletion state gates, tombstones, cleanup tracking, and stale-event no-ops |
| duplicated Speech adapter | worker `AzureSpeechTranscriptionService` behind `ISpeechTranscriptionService` |
| duplicated Blob retrieval | worker private Blob retrieval behind `IConsultationAudioBlobRetriever` / `IPrivateBlobObjectClient` |

## Verification

The normal backend test suite now includes removal guardrails:

- `ArchitecturePrivacyEnforcementTests` fails if active source/config reintroduces Functions packages, trigger attributes, host/local Functions settings, legacy direct queue names, default-exchange integration-event publishing, reflection-based event routing, host startup migrations, queue-draining APIs, or unsafe telemetry terms.
- `FailureMatrixCoverageTests` keeps the end-to-end failure matrix mapped to automated backend evidence.
- Repository search over active source/config finds no `Microsoft.Azure.Functions*`, `[RabbitMQTrigger]`, `host.json`, `consultation.processing`, or legacy direct `consultation.transcript` queue configuration.

Documentation may still describe the rejected Function/direct-queue path as historical context. The guardrails intentionally scan active source/config instead of design history.
