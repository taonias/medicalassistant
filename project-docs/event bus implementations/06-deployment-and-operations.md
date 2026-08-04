# Deployment and Operations Model

## Deployment units

| Unit | Runtime shape | Scaling signal |
| --- | --- | --- |
| Main Backend | HTTP container plus outbox relay and Transcript Ready consumer | HTTP latency/CPU plus outbox and subscriber queue age |
| Transcription Worker | Headless .NET Worker container | Audio queue age, Azure Speech quota, memory, database capacity |
| Clinical Knowledge | Existing API/background ingestion container | Ingestion queue age, model/embedding quota, database capacity |
| RabbitMQ | Durable broker service | Connection/channel count, memory/disk alarms, queue age/depth |
| PostgreSQL | Durable database service | transactions, locks, connections, storage, replica/backup health |
| Blob Storage | Private managed storage or local emulator | request failures, latency, capacity |

## Local and integration orchestration

Create one root Compose model rather than requiring developers to start the current RabbitMQ Compose and every application separately. It should provide:

- a durable RabbitMQ node with management UI limited to local use;
- PostgreSQL databases and health checks;
- local Blob Storage/emulator or an explicitly configured development account;
- Main Backend, Transcription Worker, and Clinical Knowledge containers;
- internal service DNS, named volumes, and a dedicated network;
- optional observability collector/dashboard profiles;
- deterministic test credentials sourced from an ignored environment file, never hard-coded production secrets.

Compose dependency declarations improve startup ergonomics but do not replace retry/recovery logic. Containers must tolerate RabbitMQ, PostgreSQL, Blob Storage, or downstream APIs becoming temporarily unavailable.

## Production requirements

- Run RabbitMQ as a durable, monitored service appropriate to the required availability; use quorum queues when the production cluster supports and operationally owns them.
- Use TLS for AMQP and separate least-privilege credentials for backend publication/consumption, Transcription Worker consumption/publication, and operators.
- Put each environment in a separate virtual host or broker cluster according to platform policy.
- Give the worker a restricted database role and storage identity. It does not receive general backend credentials.
- Keep management UI and metrics endpoints off the public network.
- Store broker, database, Blob, Speech, and API secrets in the platform secret store with rotation procedures.
- Back up broker definitions/policies and durable databases. Queue contents are not a substitute for the database outbox/source of truth.

## Topology ownership

The event-bus infrastructure idempotently declares `medicalassistant.events`. Each subscriber declares its own main, retry, and dead-letter queues and bindings during startup or through an equivalent controlled provisioning step. Exchange/routing contracts are shared; queue names and retry implementation belong to the subscriber.

Changing immutable RabbitMQ queue arguments in place can fail startup. Such changes use a new queue/topology name and a controlled migration instead of silently deleting the existing durable queue.

## Safe deployment order

1. Apply expand-only database migrations through the single migration job.
2. Provision/validate broker policies, exchange, permissions, and observability.
3. Deploy producers capable of writing the new outbox records while publication remains feature-controlled.
4. Deploy consumers with consumption disabled or bindings isolated until cutover approval.
5. Enable the new route according to the migration runbook.
6. Observe queue age, processing outcomes, outbox lag, duplicate no-ops, and dead letters.
7. Perform contract/schema cleanup only after all old application versions are gone.

## Health and alerts

Required alerts include:

- outbox oldest-unpublished age and failed publish attempts;
- main/retry/dead-letter queue depth and oldest-message age per subscriber;
- consumer reconnect loops and acknowledgement latency;
- transcription success/failure/latency and Azure Speech throttling;
- duplicate and stale-event no-op rates;
- database transaction/lock failures and connection-pool pressure;
- pending deletion cleanup age;
- Clinical Knowledge handoff and reconciliation failures.

Liveness never depends on a remote service. Readiness reports whether an instance should receive work, while dependency degradation is separately observable so orchestrators do not create restart storms during an external outage.

Implemented service probes:

- `GET /health/live` on the backend API returns process liveness only.
- `GET /health/ready` on the backend API runs checks tagged `ready`, including RabbitMQ subscriber topology configuration.
- The standalone Transcription Worker registers a readiness check that verifies RabbitMQ subscriber identity, queue configuration, and the Audio Uploaded subscription without opening a dependency probe on every check.

Initial safe metric instruments:

| Meter | Instrument | Safe dimensions |
| --- | --- | --- |
| `MedicalAssistant.EventBusRabbitMQ` | `medicalassistant.eventbus.deliveries.handled` | `event.type`, `outcome` |
| `MedicalAssistant.EventBusRabbitMQ` | `medicalassistant.eventbus.deliveries.routed` | `event.type`, `queue.role`, `retry.attempt` |
| `MedicalAssistant.ConsultationOutbox` | `medicalassistant.outbox.messages.claimed` | none |
| `MedicalAssistant.ConsultationOutbox` | `medicalassistant.outbox.messages.published` | `event.type` |
| `MedicalAssistant.ConsultationOutbox` | `medicalassistant.outbox.messages.failed` | `event.type`, `failure.category` |

These metrics deliberately exclude Consultation ID, file names, blob/object references, transcript text/previews, raw exception messages, provider response bodies, and secrets.

## Scaling rules

Scale workers primarily from oldest eligible queue age, not raw message count alone. Set a bounded per-instance prefetch/concurrency value, measure audio memory footprint and speech-provider limits, and increase one constraint at a time. A worker that begins shutdown stops pulling new messages, drains active handlers for a bounded period, and leaves uncommitted work unacknowledged for another instance.

## No-Functions completion criteria

The target is not complete while any of the following remains required in a deployed environment:

- Azure Functions runtime or Function App resource
- `Microsoft.Azure.Functions.Worker*` packages
- RabbitMQ Functions trigger extension or `[RabbitMQTrigger]`
- `host.json` or Functions-specific settings/deployment pipeline
- legacy direct queues used only by the Function

Removal happens only after the migration and reconciliation checks prove that no consultation work is stranded.
