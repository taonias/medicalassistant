# Security, Privacy, and Clinical-Data Controls

## Security objective

The event bus coordinates clinical processing without becoming a second clinical-content store. Events identify facts and authorized resources; audio and transcript content stay in dedicated encrypted stores and cross only approved service boundaries.

This document defines technical controls, not a legal retention determination. Privacy/security/compliance owners must approve environment-specific retention, erasure, residency, and access policies.

## Data classification by location

| Location | Allowed | Prohibited |
| --- | --- | --- |
| RabbitMQ message | Event/routing metadata, Consultation/file/transcript IDs, opaque storage reference, content type, duration/counts, stable failure category | Audio, transcript text/preview, original patient-bearing filename, credentials, signed URL, provider error body |
| Outbox | Same minimal event envelope/payload needed for durable publication | Additional domain entity snapshot, clinical text, secrets |
| Inbox | Consumer/event identity, state, attempts/timestamps, safe outcome category | Event payload copy, transcript/audio, raw exception |
| Logs/traces/metrics | IDs approved for operational correlation, type/version, attempt, sizes/counts, timing, stable outcome | Serialized payload, blob URI, transcript preview, filenames, speech/provider bodies, secrets, high-cardinality clinical text |
| DLQ | Original minimal event required for recovery | Operator annotations containing patient content or copied transcript/audio |
| Database/Blob | Authorized clinical records/content under application policy | Public access or broad cross-service credentials |

Identifiers can still be sensitive/linkable. Their presence is minimized, access-controlled, retained briefly where practical, and never used as metric dimensions when cardinality/privacy would be unsafe.

## Service identities and authorization

Use separate non-human identities:

- **Backend**: publish consultation events; consume its Transcript Ready/deletion cleanup queues; access authorized application data/outbox/inbox; call Clinical Knowledge.
- **Transcription Worker**: consume only its audio/retry queues; publish permitted result events; read private consultation audio; access only required Consultation Processing tables.
- **Clinical Knowledge**: accept authenticated backend calls; it does not receive RabbitMQ or backend database credentials under the accepted design.
- **Operations/replay**: separate audited role with read/diagnose and narrowly scoped replay permissions; application identities cannot use management administration APIs.

RabbitMQ permissions restrict configure/write/read separately by exchange/queue naming patterns and virtual host. PostgreSQL grants should be verified with negative integration tests, not assumed from connection-string separation.

### RabbitMQ service identity map

The checked-in configuration names the workload identity expected by each RabbitMQ connection. Password values in checked-in settings are placeholders and must be replaced by the deployment secret store or local ignored environment files.

| Workload | Example identity | Allowed broker action |
| --- | --- | --- |
| Backend Clinical Knowledge consumer | `backend-clinical-knowledge` | Read/configure only the backend-owned Transcript Ready/deletion subscriber queue and retry/DLQ topology; no access to transcription-worker queues. |
| Backend outbox relay / legacy publisher compatibility | `backend-outbox-relay` | Publish consultation integration events to `medicalassistant.events`; no subscriber-queue read permission. |
| Transcription Worker | `transcription-worker` | Read/configure only the audio-upload subscriber queue and retry/DLQ topology; publish Transcript Ready through the outbox path. |
| Broker bootstrap/operator | `medicalassistant-broker-bootstrap` | Local bootstrap/admin only; never used by application workloads. |

The event-bus connection options reject blank credentials and shared default broker users such as `guest` or `admin`. Production AMQP should set `RabbitMQ:UseTls=true` and, when the certificate name differs from the broker host, `RabbitMQ:TlsServerName`.

## Transport and storage protection

- TLS for production AMQP, PostgreSQL, Blob, Speech, and internal HTTP; validate certificates/hostnames.
- Encryption at rest and platform-managed backup protection for databases, Blob Storage, and broker durable volumes/backups.
- Disable public Blob access. Prefer workload identity; otherwise use short-lived credentials from the secret store.
- Event payloads use opaque object references, never public or long-lived signed URLs.
- Management UI, metrics, health details, and database/broker ports remain on protected networks.
- Apply resource limits to event size, audio size/duration, HTTP bodies, decompression, concurrency, and provider timeouts.

## Secret handling

Secrets are injected at runtime from the deployment secret store. They never appear in source-controlled settings, Compose defaults intended for production, RabbitMQ messages, outbox/inbox fields, command-line arguments, logs, traces, health output, crash reports, or test snapshots.

Rotation procedures support broker credentials, database credentials, Blob identity/key, Azure Speech credential, Clinical Knowledge API secret, and telemetry exporter credential. Rotation is rehearsed and does not require reintroducing shared administrator credentials.

## Telemetry allowlist

Telemetry is allowlisted rather than redacted after serialization. Safe examples:

- service/environment/instance identity;
- event type/version and event/correlation/causation IDs where approved;
- Consultation ID only in protected logs/traces, never metric labels;
- attempt, queue role (main/retry/DLQ), outcome/failure category;
- byte/character counts, duration, locale, and dependency status code category;
- outbox/queue age and depth.

The target removes current Function diagnostics that record blob/file names and URIs, transcript previews, complete parsed messages, exception messages, and Azure Speech response bodies.

RabbitMQ retry and dead-letter forwarding preserves only the W3C trace headers plus the internal retry-attempt header. Synthetic canary tests cover original filenames, blob/object paths, transcript previews, provider error bodies, and authorization secrets so these values do not become durable broker headers.

Implemented privacy guardrail:

- Active event-processing logs and telemetry are scanned for forbidden payload/PHI terms such as transcript text, storage object references, blob URIs, filenames, provider bodies, signed URLs, payload bodies, and storage account keys.
- Retry/DLQ header forwarding is already covered by synthetic canary tests that prove trace context and retry attempt survive while PHI/secret canaries are dropped.
- Architecture enforcement excludes documentation and generated/build output so rejected patterns can remain documented without making active-code scans meaningless.

## Failure and DLQ privacy

Dead-letter queues and outbox retries may retain event payloads longer than the happy path, so they require explicit maximum retention, capacity alarms, encrypted durable storage, restricted operator access, and deletion procedures. Incident tickets receive safe metadata and a protected internal lookup link/ID—not copied payloads.

Malformed messages may be attacker-controlled. Never interpolate raw payloads/headers into logs, metrics, SQL, shell commands, or management annotations.

## Deletion and retention

Deletion stops new processing through authoritative state gates and triggers idempotent Blob/Clinical Knowledge cleanup. A content-free tombstone prevents delayed events from restoring content. Retention applies separately to:

- clinical database records and audit;
- audio/document blobs and versions;
- transcript content and revisions;
- Clinical Knowledge chunks/embeddings/payloads;
- outbox/inbox rows;
- main/retry/dead-letter messages;
- broker/database backups;
- logs, traces, metrics, and incident records.

The system must be able to report pending cleanup and reconcile it. A `Deleted` API response alone is not proof that all asynchronous copies have been removed.

## Threat-focused controls

- **Forged event**: broker authentication/ACLs, schema validation, stable producer identity, reject unsupported contract/version.
- **Replay/duplicate**: inbox event-ID uniqueness plus authoritative state/revision gates.
- **Queue flooding**: publisher ACLs, message-size limits, quotas/alarms, bounded prefetch and concurrency.
- **Poison message loop**: immediate DLQ for invalid contracts, bounded delayed retries for transient failures.
- **Cross-patient processing**: do not trust patient/doctor context from unnecessary event fields; load authoritative context in the backend and verify Consultation relationships.
- **Stale event after deletion/correction**: tombstone/current revision wins over event arrival order.
- **Credential compromise**: least privilege, separate identities, short-lived/rotatable secrets, audit and revocation playbook.
- **Telemetry exfiltration**: allowlist attributes, synthetic canary leakage tests, protected exporters, retention/access controls.

## Security review checklist

- Threat model covers broker, outbox/inbox, worker, Blob/Speech, Clinical Knowledge handoff, DLQ/replay, and deletion.
- RabbitMQ/PostgreSQL/Blob negative authorization tests pass for every service identity.
- TLS and certificate validation are enabled outside local-only profiles.
- No default/admin credentials or publicly reachable management endpoints remain.
- PHI canary tests pass across logs, traces, metrics, audit, incidents, and DLQ tooling.
- Retention/erasure rules and operator access are approved and implemented per environment.
- Backup/restore and credential revocation/rotation procedures are tested.
