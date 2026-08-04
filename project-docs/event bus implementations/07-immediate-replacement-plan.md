# Immediate Replacement and Implementation Plan

## Migration premise

The existing Azure Function code has never run in a deployed environment. This is a pre-production architecture replacement, not a live-system migration. Build and test the new path from empty infrastructure, then remove the unused Function code in the same implementation effort.

## Target change sequence

### 1. Establish shared event-bus modules

- Add broker-independent integration-event envelope, stable routing-name mapping, typed handlers, subscriptions, and `IEventBus` publication abstraction.
- Add RabbitMQ transport with one `medicalassistant.events` direct exchange, subscriber-owned durable queues, confirms/mandatory-return handling, trace headers, retry queues, dead-letter queues, and manual acknowledgement.
- Register the RabbitMQ consumer as a normal `IHostedService`, following the useful eShop composition pattern.
- Add serialization/contract tests before connecting business handlers.

### 2. Add durable persistence primitives

- Add outbox records with event ID/type, occurred/correlation/causation metadata, serialized minimal payload, state, attempt count, lease owner/expiry, next-attempt time, timestamps, and last safe failure category.
- Add inbox records unique by `(consumerName, eventId)` with processing/completed outcome metadata.
- Add Transcript revision/concurrency support and any stable source-file identity required by the accepted contracts.
- Add deletion tombstone/cleanup tracking without clinical content.
- Apply the schema only through the one migration job; keep application startup migration-free.

### 3. Replace backend direct publishers

- Upload commands atomically persist Consultation state and either `consultation.audio-uploaded.v1` or `consultation.document-uploaded.v1` in the outbox.
- Transcript edits atomically increment revision and outbox `consultation.transcript-ready.v1`.
- Consultation deletion atomically records the tombstone/cleanup intent and outbox `consultation.deleted.v1`.
- Remove best-effort request-thread RabbitMQ calls and all publication to the default exchange/legacy named queues.
- Remove queue-draining deletion behavior.

### 4. Create the standalone Transcription Worker

- Create a standard .NET Worker host and reference shared Consultation Processing/application persistence plus event-bus modules.
- Port/adapt Azure Speech and private Blob retrieval behind application interfaces.
- Implement the typed Audio Uploaded handler, state gates, idempotent inbox, atomic result/outbox transaction, safe failure classification, cancellation, and graceful shutdown.
- Bind only `consultation.audio-uploaded.v1`; never create placeholder text for documents.
- Use restricted database/storage/broker credentials and PHI-safe OpenTelemetry.

### 5. Add the backend Transcript Ready consumer

- Bind a backend-owned queue to `consultation.transcript-ready.v1`.
- Load the current authorized Transcript and consultation context after checking deletion/revision state.
- Submit the existing Clinical Knowledge `SessionTranscript` request without putting transcript text on RabbitMQ.
- Persist ingestion identity/inbox outcome and reconcile uncertain HTTP outcomes idempotently.

### 6. Add deletion cleanup subscribers

- Make stale Audio Uploaded and Transcript Ready deliveries successful no-ops based on current state.
- Delete private blobs and un-ingest Clinical Knowledge content idempotently.
- Track incomplete cleanup and expose replay/alerting.

### 7. Add container orchestration and operations

- Add Dockerfiles and root Compose services/health checks for the backend, worker, broker, database, blob emulator/development dependency, and Clinical Knowledge service.
- Add topology/policy initialization where controlled provisioning is preferred.
- Add dashboards/alerts for outbox lag, queue age, retries, dead letters, transcription outcomes, and cleanup convergence.

### 8. Remove the Azure Function and legacy path

After the new integration and end-to-end tests pass, delete:

- the `transcriber` Azure Functions project and its Function entry point;
- `Microsoft.Azure.Functions.Worker*` and RabbitMQ trigger-extension references;
- `host.json`, Functions local settings/examples, and Functions deployment instructions;
- duplicate direct RabbitMQ publishers in the old transcriber and backend;
- legacy `consultation.processing` and `consultation.transcript` configuration/topology;
- the queue-scanning deletion method and associated tests/docs.

Preserve reusable speech/blob logic by moving it into the new worker before deleting the old project.

Implemented removal is recorded in `13-legacy-removal-verification.md`. The active Azure Function project and backend direct RabbitMQ publishers have been deleted; reusable Speech and Blob behavior is now behind the standalone worker seams.

## No migration work required

Because there is no deployed history:

- do not dual-publish old and new events;
- do not create an old-to-new queue bridge;
- do not backfill Transcript Ready events;
- do not carry forward legacy queue messages or Function execution history;
- do not add compatibility flags that can accidentally reactivate the old route.

Local RabbitMQ/PostgreSQL volumes used during development should be recreated or explicitly migrated to the new empty schema/topology according to developer need; they are not production records.

## Implementation acceptance criteria

- A new audio upload commits a Consultation and outbox event even when RabbitMQ is unavailable.
- After broker recovery, the relay publishes exactly the same event ID and the worker eventually stores one completed Transcript.
- A crash/redelivery does not create a second logical Transcript or duplicate outbox outcome.
- Transcript text reaches Clinical Knowledge through the backend API boundary and never appears in RabbitMQ payloads or normal telemetry.
- Documents do not enter the Transcription Worker and do not create placeholder Transcripts.
- Deleted Consultations cannot be transcribed or re-ingested by delayed events.
- Five transient retries lead to the subscriber DLQ and alert; controlled replay succeeds after repair.
- Root Compose starts a clean end-to-end environment without Azure Functions tooling.
- Repository search finds no Functions SDK/trigger/host artifacts or legacy queue configuration in active code.
- All unit, contract, database integration, RabbitMQ integration, failure-injection, security-log, and end-to-end tests pass.
