# Testing and Verification Strategy

## Test layers

### Unit tests

- Explicit CLR event type → routing key mapping and duplicate-name rejection
- Envelope validation, version dispatch, tolerant-reader behavior, and safe failure classification
- Consultation status/revision/deletion gates
- Audio-only routing and rejection of document work by the Transcription Worker
- Inbox duplicate/stale-event decisions
- Outbox retry/backoff/lease-expiry state transitions
- Transcript completion/failure and clinician-correction rules
- Redaction helpers and telemetry attribute allowlist

Azure Speech, Blob Storage, clock, event bus, and unit of work remain behind interfaces for deterministic tests. Unit tests do not pretend to prove RabbitMQ acknowledgement or PostgreSQL transaction behavior.

### Serialization contract tests

For every supported event version:

- serialize representative producer events to committed JSON snapshots/schema examples;
- deserialize using each consumer contract;
- reject missing required fields, unsupported major versions, invalid identifiers, oversized input, and unexpected semantic values;
- prove optional additions are ignored/defaulted safely by older tolerant readers;
- assert audio/transcript bytes/text, patient/doctor IDs, original filenames, credentials, signed URLs, and exception details are absent.

Contract tests use routing keys as external names and remain green when CLR types/namespaces are refactored.

### PostgreSQL integration tests

Run against a real disposable PostgreSQL instance:

- Consultation state and outgoing event commit or roll back together.
- Inbox, Transcript, status, audit, and result outbox commit atomically.
- Unique inbox constraints resolve concurrent duplicate deliveries safely.
- Outbox claim/skip-locked or lease behavior works across multiple relay instances.
- Expired claims recover after simulated process death.
- Transcript concurrency/revision prevents stale overwrite.
- Deleted Consultation state wins races with completion.
- The worker database role cannot read/write unrelated identity or application data.

### RabbitMQ integration tests

Run against a real disposable RabbitMQ node:

- Direct exchange bindings deliver audio only to the Transcription Worker queue.
- Document events never reach that queue.
- Persistent publish with mandatory routing receives confirm; an unbound routing key is detected as returned/unroutable.
- Handler ACK occurs only after the durable completion signal.
- Connection/channel loss causes recovery without losing an unacknowledged event.
- Five delayed retries preserve event identity and end in the correct DLQ.
- Duplicate publication produces one business effect.
- Multiple subscriber queues receive the same fact independently.
- Trace headers propagate without payload logging.

### Dependency adapter tests

- Blob retrieval uses private authorization, opaque object references, size/content-type validation, bounded reads, and cancellation.
- Azure Speech adapter maps throttling/timeouts/server errors to transient categories and invalid/unsupported audio to stable terminal categories.
- Provider response bodies and speech content never enter normal logs/audit.
- Clinical Knowledge client uses authentication, timeouts, stable document identity, and reconciliation after an uncertain response.

Live cloud-provider tests are a separate protected pipeline/profile and use synthetic, non-patient fixtures.

## End-to-end scenarios

Root Compose tests must prove:

1. **Happy audio path**: upload → outbox → RabbitMQ → worker → Transcript Ready → backend → Clinical Knowledge durable ingestion.
2. **Broker outage at upload**: accepted upload remains in outbox and completes after broker recovery.
3. **Worker crash before commit**: redelivery completes once.
4. **Worker crash after commit/before ACK**: inbox duplicate no-op.
5. **Speech transient outage**: five delayed retries, DLQ, alert, repair, controlled replay.
6. **Permanent invalid audio**: terminal failure state/event, ACK, no DLQ loop.
7. **Document upload**: Document Uploaded event exists; no Transcription Worker delivery or placeholder Transcript.
8. **Deletion race**: delete during speech; no Transcript/knowledge content survives or is recreated.
9. **Transcript correction**: revision event supersedes the existing Clinical Knowledge document idempotently.
10. **Backend/AI outage**: Transcript remains complete while its ready event retries and later ingests.
11. **Scale-out**: concurrent worker/relay replicas do not duplicate business effects.
12. **Graceful shutdown**: active work drains or redelivers without partial commit.

The scenario-to-evidence map lives in `12-end-to-end-failure-matrix.md`. `FailureMatrixCoverageTests` keeps that matrix executable by checking that every scenario has named automated backend evidence and that the documentation has no placeholder outcomes. Live Compose execution still records the real process/container/broker result for each scenario before release.

## PHI leakage verification

Use unique synthetic canary strings in filenames, blob paths, audio transcript, exception/provider-body fixtures, and event fields that are intentionally forbidden. Capture application logs, OpenTelemetry spans, metrics labels, audit metadata, DLQ incident output, and test reports. Fail the test if any canary appears outside approved encrypted clinical storage/test fixtures.

## Architecture enforcement

CI checks should fail when active source/config contains:

- Azure Functions SDK, `ConfigureFunctionsWorkerDefaults`, `[Function]`, or `[RabbitMQTrigger]`;
- `host.json` or Function-specific deployment settings;
- legacy `consultation.processing` / `consultation.transcript` queue configuration;
- direct publication to the RabbitMQ default exchange for integration events;
- routing based on `typeof(T).Name`;
- logs/traces containing serialized event bodies or transcript previews;
- queue-draining deletion code.

Allowlists must be narrow enough that documentation/history samples do not accidentally hide active-code violations.

Implemented enforcement:

- `ArchitecturePrivacyEnforcementTests` scans active source/config, not project documentation, for Functions artifacts, legacy direct queues, default-exchange integration publishing, reflection-based event routing, automatic host migrations, queue-draining APIs, and unsafe telemetry/logging terms.
- The obsolete `transcriber` Azure Function project and legacy direct RabbitMQ publisher contracts/classes have been removed from active code.
- The guardrail deliberately allows RabbitMQ retry/DLQ forwarding inside `RabbitMqHostedConsumer`, because that default-exchange publish is internal queue forwarding, not producer integration-event publication.
- Application hosts are checked for migration calls; schema changes remain owned by the dedicated migrations unit.

## Release gates

- All unit, contract, database, RabbitMQ, adapter, and end-to-end suites pass.
- Failure-injection scenarios produce the documented state/ACK/DLQ outcomes.
- Clean root Compose startup succeeds with no Functions tooling installed.
- Schema migration forward/rollback or forward-fix procedure is rehearsed on a disposable environment.
- Security review verifies identities, ACLs, secret handling, telemetry allowlist, retention, and replay authorization.
- Operations review confirms dashboards, alerts, DLQ/replay procedure, backup/restore, and ownership.
- Repository search proves the no-Functions completion criteria.

The executable clean-environment handoff checklist lives in `14-clean-environment-release-gate.md`. It records the exact local commands, stop-release policy, required release evidence, security/operations review, dashboard/alert coverage, backup/restore rehearsal, and no-Functions scan expected before the first deployable release.
