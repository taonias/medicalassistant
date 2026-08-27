# Standalone Transcription Worker Design

## Purpose

The Transcription Worker turns an eligible private audio Consultation File into a stored Transcript. It is a normal .NET Worker process: no Azure Functions SDK, Functions host, trigger attributes, `host.json`, or Functions deployment artifact remains.

## Processing algorithm

1. Receive `consultation.audio-uploaded.v1` through the shared RabbitMQ hosted consumer.
2. Validate the envelope and supported contract version. Invalid contracts dead-letter without processing.
3. Open a scoped unit of work and inspect the inbox by `eventId`.
4. If already completed, acknowledge as a duplicate.
5. Load the Consultation and verify that its current state/revision remains processable.
6. Record processing started without logging payload content, then commit before the external speech call if product status must be visible.
7. Retrieve the private blob using an opaque object reference and a configured storage connection string.
8. Transcribe eligible audio through Azure Speech with bounded timeout and cancellation.
9. Begin the completion transaction and recheck Consultation deletion/revision state.
10. Store the Transcript, advance Consultation status, write content-free audit metadata, mark the inbox completed, and add either Transcript Ready or Transcription Failed to the outbox.
11. Commit and acknowledge the RabbitMQ delivery.

Transient exceptions before the completion commit currently dead-letter immediately — the delayed-retry mechanism exists in code but ships disabled (`RabbitMQ:Topology:RetryDelays` is empty; tracked as [K07](../../docs/known-issues/refactor-baseline.md)). A valid audio file that Azure Speech definitively cannot process is committed as `Transcription Failed` and acknowledged. A process crash can repeat the Azure Speech call, but the completion transaction remains idempotent.

## Migration of current code

| Current Functions code | Target |
| --- | --- |
| `ConfigureFunctionsWorkerDefaults()` | Standard Generic Host / `Host.CreateApplicationBuilder` |
| `[Function]` and `[RabbitMQTrigger]` | Typed `ConsultationAudioUploadedIntegrationEventHandler` invoked by the RabbitMQ hosted consumer |
| `ProcessConsultationFileFunction` orchestration | Application handler with explicit classification and unit-of-work boundaries |
| Function host acknowledgement | Event bus acknowledges only after the handler reports durable success |
| Direct `RabbitMqTranscriptReadyPublisher` | Outbox entry published by the shared relay |
| Manually addressed `consultation.transcript` queue | `consultation.transcript-ready.v1` on `medicalassistant.events` |
| Duplicate check based only on transcript/status | Inbox uniqueness by event ID plus transcript revision/state checks |
| `TranscriberDbContext`-specific persistence | Shared Consultation Processing persistence module and transaction boundary |
| Functions Application Insights integration | OpenTelemetry worker instrumentation with content-free attributes |
| `host.json` and `local.settings.json` | Normal app configuration, environment variables, and secret-provider integration |

The existing `AzureSpeechTranscriptionService` and blob retrieval behavior can be adapted behind application interfaces. Retry classification, cancellation, content limits, authentication, and safe diagnostics must be tightened rather than copied verbatim.

## Atomic completion

The completion transaction contains:

- an inbox processing result keyed by the incoming event ID;
- the Transcript insert/update and monotonically increasing revision;
- the Consultation status transition;
- content-free processing audit metadata;
- one outgoing Transcript Ready or Transcription Failed outbox record.

Direct RabbitMQ publication never occurs inside the handler. The outbox relay publishes after commit.

## Concurrency

A unique inbox constraint prevents the same event ID being completed twice. A Transcript/Consultation concurrency token prevents two distinct events from silently overwriting one another. Worker prefetch and parallelism are configurable and start conservatively because audio buffers, Azure Speech quotas, and database connections—not CPU alone—set the safe ceiling.

On shutdown the worker stops accepting new deliveries, allows active handlers a bounded drain period, and leaves any uncommitted/unacknowledged message for redelivery.

## Configuration

Required configuration categories:

- RabbitMQ endpoint, TLS trust, virtual host, worker credential, queue identity, prefetch, and retry policy
- PostgreSQL connection using the restricted worker role
- Blob Storage account/container authorization without public or long-lived signed URLs
- Azure Speech endpoint/region, credential, locale, phrase-list source, timeout, and request-size limits
- OpenTelemetry exporter endpoint and environment/service identity
- Health thresholds, shutdown drain period, and feature flag controlling consumption during cutover

Secrets are supplied by the deployment secret store and never committed to `appsettings.json`, local settings, events, logs, or traces.

## Health model

- **Liveness**: process and hosted consumer loop are running; no dependency call is required.
- **Readiness**: database schema is compatible, RabbitMQ channel/topology is ready, and required configuration is valid. Blob/Speech probes should be lightweight and cached rather than run on every health request.
- **Degraded signals**: rising queue age, retry volume, outbox age, dependency throttling, or DLQ depth alert operators without forcing process restarts.

## PHI-safe diagnostics

The current Function records filenames, blob URIs, provider response bodies, transcript previews, and exception messages in several audit/log paths. Those fields are prohibited in the target. Allowed operational fields include event ID/type, correlation/causation IDs, Consultation ID, byte/character counts, locale, attempt, stable failure code, elapsed time, and outcome. Exception objects may be captured only by an approved protected error system with redaction; normal structured logs receive a stable category instead.

## Non-audio documents

The current Function creates placeholder Transcript text for non-audio documents. The target removes that behavior. The worker binds only `consultation.audio-uploaded.v1`; it never receives `consultation.document-uploaded.v1`. Documents remain visibly pending until a separate Document Processing handler extracts their content, and no fake Transcript is created.
