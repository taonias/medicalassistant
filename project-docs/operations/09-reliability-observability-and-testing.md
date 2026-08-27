# Reliability, Observability, and Testing

> Historical review snapshot (31 July 2026). Use [messaging-and-recovery.md](../../docs/runbooks/messaging-and-recovery.md) for current reliability/recovery mechanics and [cross-system-quality.md](../../docs/modules/cross-system-quality.md) for the current test/architecture-enforcement inventory.

## Reliability model by component

### Main backend

- Applies main persistence migrations during startup and seeds Identity roles.
- Uses EF Core provider retry policies for PostgreSQL/SQL Server connections.
- Supports idempotent consultation creation per doctor and idempotency key.
- Logs API requests with Serilog and translates exceptions through middleware.
- Treats RabbitMQ publication as best-effort: a broker failure does not fail the upload command.
- Treats note/structured-data AI indexing as best-effort.
- Treats transcript-ready publication after manual edit as best-effort.

The best-effort choices protect the clinician's primary save/upload action, but there is no durable outbox or replay ledger. They trade immediate availability for the possibility of permanently missing downstream work.

### Transcriber

- RabbitMQ delivery is at least once.
- A successful function invocation acknowledges/removes the inbound message.
- Failures are rethrown so the broker/runtime can retry.
- Existing completed transcripts are recognized and skipped on redelivery.
- The duplicate path republishes the transcript-ready event before acknowledging.
- One transcript per consultation is enforced by a unique index.
- Function timeout is ten minutes; queue prefetch is one.

There is no configured dead-letter policy in the included RabbitMQ definition. Poison messages can repeatedly retry unless the platform adds limits/dead-lettering.

### AI service

- Persists ingestion state before placing work on an in-process channel.
- Claims work with PostgreSQL advisory locks.
- Runs multiple configurable workers but validates that their connection demand cannot starve the pool.
- A recovery sweep re-enqueues abandoned non-terminal ingestions.
- Attempts are capped to avoid endless poison loops; failed work can be manually retried.
- Corrections and derived data commit atomically.
- Database migrations use an advisory lock for safe concurrent startup.
- Embedding dimensions are validated at startup against the fixed vector schema.
- Duplicate and concurrent same-identity submissions have explicit outcomes.

The channel itself is not durable, but the ingestion row is; durability comes from recovery, not from the in-memory queue.

## Failure semantics visible to users

### Consultation workflow

The frontend polls consultation/transcript data and can display processing/failure states. A lost RabbitMQ publish is problematic because the consultation may remain `AudioUploaded` or `DocumentUploaded` without a durable job/failure record explaining that no worker will see it.

### AI ingestion

The caller receives an ingestion ID and can poll `Queued`, `Processing`, `Completed`, `Failed`, `Superseded`, or `Deleted`. A quality report exists only for completed work. Failed ingestions preserve payload for retry.

### AI grounded answer

- No evidence above threshold: normal HTTP 200 refusal.
- Invalid/blank question: HTTP 400.
- Invalid secret: 401 (or 403 for non-admin erasure).
- Fabricated citation label: HTTP 500 with answer text discarded.
- Provider/database failures: 5xx after provider/library retry behavior.

## Observability

### Main backend

Serilog writes console/request logs. Audit and error repositories exist, but their use is not uniformly visible across every feature. No health/readiness endpoint or metrics exporter was found.

### Transcriber

Application Insights is configured. The function writes detailed audit actions for start, message parsing, blob retrieval, speech, transcript save, queue publish, completion, duplicate skip, and failure.

Privacy caveat: transcript previews are included in some audit details. That may help debugging but expands PHI in logs/audits.

### AI service

OpenTelemetry instruments ASP.NET Core, HTTP, Npgsql, ingestion spans/metrics, and structured logs. OTLP export activates when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. The service deliberately records identifiers and counts, not clinical text.

Each completed ingestion also persists a quality report with chunk/token and guardrail metrics. This supports comparison against fixed golden documents even when a run did not technically fail.

## Test inventory

### Main backend

Static source review found:

- 9 `[Fact]` declarations across 2 test source files.
- Application validation coverage for feature validators.
- A persistence integration project whose only source file remains `UnitTest1.cs`.

Missing or sparse areas include API authorization, repository ownership boundaries, upload/blob behavior, RabbitMQ publication, callback idempotency, transcript lifecycle, structured-data approval, and end-to-end doctor workflows.

### AI service

Static source review found 53 test source files, 166 `[Fact]` and 2 `[Theory]` declarations. The suite covers:

- Authentication and request validation.
- Strategy routing and document types.
- Duplicates, continuations, corrections, and concurrency.
- Retry, recovery, startup races, and schema migrations.
- PDF intake/extraction, lab panels/analytes, imaging reports, and summaries.
- Un-ingestion and erasure.
- Status events/SignalR and backfill.
- Retrieval scope, search, ranking, refinement, threshold/refusal, cross-language behavior, generation, and citation labels.
- OpenAI/Azure provider wiring.

Tests use the public HTTP boundary, real PostgreSQL/pgvector via Testcontainers, and controlled fake model/extraction providers.

### Frontend and transcriber

No test/spec files were found. High-value missing coverage includes:

- Recording lifecycle and browser media edge cases.
- Patient/consultation route behavior and query invalidation.
- Auth persistence and 401 handling.
- Structured-data parsing/rendering.
- Queue message contract compatibility.
- Transcriber duplicate, status, blob, speech, and audit behavior.

## What this review did not verify

Tests were not executed during documentation creation. Source counts indicate intent and coverage shape, not a green build. Runtime provider credentials and required infrastructure were not available in the documentation workspace.

## Recommended operational additions

1. Add health/readiness endpoints for database, broker, storage, and required providers.
2. Add a durable outbox/replay path for backend integration events.
3. Configure dead-letter queues and poison-message runbooks.
4. Define service-level indicators: upload-to-transcript latency, ingestion completion rate, queue age, refusal rate, citation verification failures, and patient-scope violations.
5. Add cross-service contract tests for queue messages and the new AI adapter.
6. Add backup/restore drills for both databases and Blob Storage.
7. Add synthetic end-to-end tests with non-PHI fixtures for Greek and English workflows.
8. Remove clinical text from routine logs/audits or formally govern the exception.

