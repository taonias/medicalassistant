# Current Code and Gap Analysis

> Historical pre-implementation analysis. Retained to explain design evolution; it is not the current event contract. Use the [observable interface baseline](../../docs/architecture/observable-interface-baseline.md).

## Scope inspected

This analysis combines the active backend, unused Azure Function transcriber, RabbitMQ Compose configuration, Clinical Knowledge code/design records, and existing project documentation. The user confirmed the Function code has never run in a deployed environment, so these are implementation gaps rather than live-data migration risks.

## Current upload publication

Relevant code:

- [`ConsultationReadyForProcessingNotificationHandler`](../../backend/src/MedicalAssistant.Application/Notifications/ConsultationReadyForProcessingNotificationHandler.cs)
- [`ConsultationProcessingMessage`](../../backend/src/MedicalAssistant.Application/Models/Messaging/ConsultationProcessingMessage.cs)
- [`RabbitMqConsultationProcessingPublisher`](../../backend/src/MedicalAssistant.Infrastructure/Messaging/RabbitMqConsultationProcessingPublisher.cs)

The backend builds one generic `ConsultationFileUploaded` message for audio or document uploads. It includes patient/doctor IDs, status, file kind, Blob URI, content type, original filename, duration, correlation ID, and occurrence time. It directly publishes to the durable `consultation.processing` queue through RabbitMQ's default exchange.

Publication failure is caught and logged as a warning so the user-facing command still succeeds. Because no outbox record exists, a stored Consultation can be permanently stranded if publication fails. `mandatory: false`, no explicit publisher-confirm decision, and a newly generated Rabbit message ID on every call further weaken recovery/idempotency.

Target response:

- atomically write minimal Audio Uploaded or Document Uploaded event to the outbox;
- let a continuous relay publish to `medicalassistant.events` with mandatory routing and confirms;
- preserve the immutable integration-event ID as RabbitMQ message ID;
- exclude unnecessary patient/doctor identity, original filename, and signed/full Blob URI.

## Current deletion behavior

Relevant code:

- [`DeleteConsultationCommandHandler`](../../backend/src/MedicalAssistant.Application/Features/Consultation/Command/DeleteConsultation/DeleteConsultationCommandHandler.cs)
- [`RabbitMqConsultationProcessingPublisher`](../../backend/src/MedicalAssistant.Infrastructure/Messaging/RabbitMqConsultationProcessingPublisher.cs)

Deletion best-effort deletes audio/document blobs, drains the entire processing queue with `BasicGet`, drops messages matching one Consultation, acknowledges every inspected delivery, then republishes the remaining messages. Malformed messages are dropped. This races consumers, changes ordering/identity, can lose work if republishing fails, and assumes the backend owns the only queue.

Target response: authoritative deleted state/tombstone plus `consultation.deleted.v1`; consumer state gates; idempotent Blob/Clinical Knowledge cleanup; no queue scanning.

## Current Azure Function transcriber

Relevant code:

- [`MedicalAssistant.Transcriber.csproj`](../../transcriber/MedicalAssistant.Transcriber.csproj)
- [`Program.cs`](../../transcriber/Program.cs)
- [`ProcessConsultationFileFunction`](../../transcriber/Functions/ProcessConsultationFileFunction.cs)
- [`TranscriptService`](../../transcriber/Services/TranscriptService.cs)
- [`AzureSpeechTranscriptionService`](../../transcriber/Services/AzureSpeechTranscriptionService.cs)
- [`RabbitMqTranscriptReadyPublisher`](../../transcriber/Services/RabbitMqTranscriptReadyPublisher.cs)

The project is a .NET 8 Worker SDK executable configured as an Azure Functions isolated worker. It uses the Functions RabbitMQ trigger extension on `consultation.processing`, retrieves Blob content, calls Azure Speech fast transcription for audio, shares the backend PostgreSQL/domain model, writes audit/status/Transcript rows, and directly publishes a result to `consultation.transcript`.

Useful behavior to preserve:

- private Blob retrieval and content metadata validation;
- Azure Speech adapter/locale/phrase configuration;
- Consultation status transitions and audit intent;
- detection of already completed Transcripts;
- shared PostgreSQL/domain access.

Behavior to replace:

- Functions SDK/host/trigger and deployment artifacts;
- acknowledgement controlled implicitly by Functions rather than the event-bus outcome;
- direct result publication outside an atomic outbox transaction;
- duplicate handling based only on current status rather than consumer/event inbox;
- multiple database updates without one documented completion transaction;
- placeholder Transcript creation for document uploads;
- audit/log details containing parsed message data, blob/file names and URIs, transcript previews, exception text, and Azure Speech response body;
- default-exchange/named-queue coupling and lack of the accepted retry/DLQ policy.

## Current Transcript Ready publication

Relevant code:

- [`UpdateTranscriptCommandHandler`](../../backend/src/MedicalAssistant.Application/Features/Transcript/Command/UpdateTranscript/UpdateTranscriptCommandHandler.cs)
- [`RabbitMqTranscriptReadyPublisher`](../../backend/src/MedicalAssistant.Infrastructure/Messaging/RabbitMqTranscriptReadyPublisher.cs)

The Function publishes Transcript Ready after initial transcription, and the backend publishes it again after clinician editing. Both publish directly to `consultation.transcript` and catch/allow failures in ways that can lose downstream work. No active repository consumer currently drains that queue.

Target response: both initial completion and corrections create revisioned `consultation.transcript-ready.v1` outbox events; the backend owns the durable consumer that retrieves current text and calls Clinical Knowledge.

## Current Clinical Knowledge boundary

Relevant sources:

- [`clinical-knowledge/CONTEXT.md`](../../clinical-knowledge/CONTEXT.md)
- [`ingestion-pipeline-design.md`](../../clinical-knowledge/docs/ingestion-pipeline-design.md)
- [`IngestionRequest`](../../clinical-knowledge/src/MedicalAssistance.Ingestion.Api/Ingestions/IngestionRequest.cs)
- [`TranscriptIngestionStrategy`](../../clinical-knowledge/src/MedicalAssistance.Ingestion.Api/Ingestions/TranscriptIngestionStrategy.cs)
- [`Program.cs`](../../clinical-knowledge/src/MedicalAssistance.Ingestion.Api/Program.cs)

The newer Clinical Knowledge service is a .NET ingestion/retrieval system whose documented only caller is the backend. `SessionTranscript` ingestion accepts inline transcript text and patient/doctor/session context over authenticated HTTP, then persists queued work and processes it through its own recovery-capable background pipeline.

This supports the accepted boundary: RabbitMQ carries only Transcript Ready identifiers/revision; the backend loads authoritative content/context and submits it to Clinical Knowledge. The Transcription Worker does not call or share data directly with the AI service.

The backend also contains an older `AiModuleHttpClient` and Python-module API document using different `/v1/...` routes. Implementation must reconcile that existing integration drift with the actual .NET Clinical Knowledge `/ingestions` contract; the event-bus change should not silently wire to obsolete endpoints.

## Current local infrastructure

[`ops/rabbitmq/docker-compose.yml`](../../ops/rabbitmq/docker-compose.yml) starts only RabbitMQ 3.13 with management UI, durable volume, configuration/plugins, environment credentials, and a health check. There is no root application Compose or Aspire AppHost.

Target response: keep the broker configuration as input but create one root container orchestration model for the complete clean environment. Aspire stays optional.

## Gap summary

| Concern | Current | Target |
| --- | --- | --- |
| Host | Azure Functions isolated worker | Standard standalone .NET Worker container |
| Publication | Request-thread best effort | Transactional outbox + continuous relay |
| Routing | Default exchange to named queues | Direct `medicalassistant.events`, explicit versioned keys |
| Consumption | Functions trigger | Typed hosted RabbitMQ consumer |
| Success ACK | Functions-host behavior | Only after durable handler commit |
| Retry | Host/broker behavior not fully specified | Five delayed retries, classification, DLQ/alert/replay |
| Idempotency | Status check | Consumer/event inbox + state/revision gates |
| Results | Direct `consultation.transcript` publish | Revisioned Transcript Ready outbox event |
| AI boundary | No queue consumer | Backend subscriber calls current Clinical Knowledge API |
| Documents | Fake placeholder Transcript | Separate Document Uploaded/Processing path |
| Deletion | Drain/rewrite queue | Deleted state/tombstone + event-driven cleanup |
| Telemetry | Payload/content-bearing details | Explicit PHI-safe attribute allowlist |
| Local orchestration | RabbitMQ only | Root Compose full environment |
| Legacy migration | Undeployed code | Direct pre-production replacement |
