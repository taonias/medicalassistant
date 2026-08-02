# PRD: Event-Bus Consultation Processing and Standalone Transcription Worker

Status: ready-for-agent

Date: 2026-08-02

## Problem Statement

A Doctor expects an uploaded Consultation Recording to reliably become an editable Transcript and then become available to Clinical Knowledge. The current design cannot provide that confidence. The backend performs best-effort direct RabbitMQ publication after storing Consultation state, so a temporary broker failure can silently strand an accepted upload. The unused transcriber depends on Azure Functions, couples publishers to named queues, publishes results outside an atomic database transaction, has no complete retry/dead-letter/replay policy, and records clinical-content-bearing details in diagnostics. Its document branch creates placeholder Transcript text even though no document content was processed. Deletion attempts to drain and rewrite a shared RabbitMQ queue, which races consumers and can lose unrelated work. No active consumer completes the Transcript Ready handoff to the current Clinical Knowledge ingestion service.

The code has never run in a deployed environment, so there is no production backlog or history to preserve. The product needs one clean, reliable initial architecture rather than a legacy migration: no Azure Functions runtime, no best-effort queue writes, no fake document Transcripts, and no hidden clinical work that operators cannot recover.

## Solution

Replace the unused Azure Function with a standalone .NET Transcription Worker and an eShop-inspired RabbitMQ integration-event layer. A Doctor's upload is accepted when the backend atomically stores the Consultation change and an outgoing event in a transactional outbox. A continuous relay publishes that immutable event to a direct exchange. The Transcription Worker consumes only Consultation Audio Uploaded, retrieves the private Recording, performs Azure Speech Transcription, and atomically commits the inbox result, Transcript, Consultation status, processing audit, and Transcript Ready outbox event before acknowledging RabbitMQ.

The backend consumes Transcript Ready, loads the current authorized Transcript plus Doctor and Patient context, and submits a Session Transcript to Clinical Knowledge over its authenticated HTTP boundary. Transcript text never travels through RabbitMQ. Consultation Documents use a separate Consultation Document Uploaded contract and remain visibly pending until a separate Document Processing capability exists.

Delivery is at least once with idempotent effects. Consumers acknowledge only after durable success, retry transient failures five times with increasing configurable delays, dead-letter exhausted or invalid deliveries, alert operators, and support controlled immutable replay. Deletion is driven by authoritative state and Consultation Deleted rather than queue manipulation. Every consumer rechecks deletion and revision state so delayed work cannot recreate obsolete clinical content.

The API, worker, RabbitMQ, PostgreSQL, Blob dependency, and Clinical Knowledge service run as normal containers with root Compose orchestration for local/integration environments. Aspire is optional. Because the Function is unused, its project, packages, triggers, settings, deployment artifacts, direct queues, and duplicate publishers are removed directly once useful Speech/Blob logic has been ported into the new worker and the new end-to-end path passes.

## User Stories

1. As a Doctor, I want an accepted Recording upload to remain scheduled when RabbitMQ is temporarily unavailable, so that I do not need to recognize or repair infrastructure failures.
2. As a Doctor, I want each eligible Recording to produce one logical Transcript, so that duplicate deliveries do not create duplicate clinical records.
3. As a Doctor, I want to see that Transcription is pending or in progress, so that asynchronous processing is understandable.
4. As a Doctor, I want a completed Transcript to remain editable, so that I can correct speech-recognition errors before relying on it clinically.
5. As a Doctor, I want each corrected Transcript revision to update the same Clinical Knowledge Document, so that obsolete text does not remain searchable beside its correction.
6. As a Doctor, I want a terminal Transcription failure to be visible as an honest outcome, so that I know the Recording needs attention.
7. As a Doctor, I want transient infrastructure failures to recover automatically, so that ordinary outages do not become manual clinical workflow tasks.
8. As a Doctor, I want a Consultation Document to remain marked as awaiting Document Processing, so that it is never represented by fake placeholder Transcript text.
9. As a Doctor, I want deleting a Consultation to stop pending Transcription, so that deleted clinical content is not recreated later.
10. As a Doctor, I want deletion to remove the corresponding Clinical Knowledge material, so that searches do not return deleted content.
11. As a Doctor, I want a delayed Audio Uploaded or Transcript Ready delivery to become a safe no-op after deletion, so that message timing cannot reverse my action.
12. As a Doctor, I want Clinical Knowledge to receive only the current authorized Transcript, so that stale revisions are not ingested.
13. As a Doctor, I want the application to remain responsive while long-running Transcription occurs independently, so that speech processing does not degrade clinician-facing HTTP traffic.
14. As a Doctor, I want clear processing outcomes without infrastructure jargon, so that retry queues and dead letters are not presented as clinical states.
15. As a Doctor, I want the new architecture to preserve the existing product workflow, so that reliability changes do not force me to learn a new upload experience.
16. As a Patient, I want my audio and Transcript content excluded from broker telemetry, so that operational systems do not unnecessarily duplicate clinical content.
17. As a Patient, I want deletion to converge across Blob Storage, the database, and Clinical Knowledge, so that an API response is backed by actual cleanup.
18. As a Patient, I want stale events prevented from restoring erased or un-ingested material, so that asynchronous recovery respects deletion.
19. As a Patient, I want service identities to have only the access needed for their role, so that one compromised worker cannot access unrelated application data.
20. As a Patient, I want audio, Transcript text, filenames, signed URLs, and provider responses absent from ordinary logs and traces, so that diagnostics minimize exposure.
21. As an Operator, I want to see the oldest unpublished outbox age, so that I can detect accepted work that has not reached RabbitMQ.
22. As an Operator, I want queue age and depth for main, retry, and dead-letter queues, so that I can distinguish healthy backlog from stuck work.
23. As an Operator, I want Transcription success, failure, duration, and Azure Speech throttling metrics, so that I can diagnose capacity and dependency problems.
24. As an Operator, I want duplicate and stale-event no-op metrics, so that I can detect unusual redelivery or ordering behavior without treating it as data corruption.
25. As an Operator, I want exhausted deliveries to create an alert with safe identifiers and failure categories, so that clinical work is never silently discarded.
26. As an Operator, I want a controlled replay procedure that preserves the original event ID and payload, so that recovery remains auditable and idempotent.
27. As an Operator, I want replay blocked until current Consultation state and contract support are checked, so that recovery cannot recreate deleted or superseded work.
28. As an Operator, I want broker outages to accumulate durable outbox work rather than fail uploads, so that service restoration can drain a reliable backlog.
29. As an Operator, I want expired outbox/inbox claims to recover after a process crash, so that a dead instance cannot permanently lock work.
30. As an Operator, I want graceful worker shutdown to stop new deliveries and drain or redeliver active work, so that deployments do not create partial results.
31. As an Operator, I want worker scaling based primarily on oldest queue age and provider capacity, so that scaling reduces user delay without overwhelming Azure Speech or PostgreSQL.
32. As an Operator, I want deletion cleanup status and oldest age, so that Blob and Clinical Knowledge cleanup failures are visible and recoverable.
33. As an Operator, I want separate broker, database, Blob, Speech, backend, and replay identities, so that permissions and credential rotation are contained.
34. As an Operator, I want protected management and metrics endpoints, so that internal topology and clinical identifiers are not publicly exposed.
35. As an Operator, I want documented procedures for broker outages, outbox stalls, Speech outages, poison messages, worker crash loops, deletion cleanup, credential rotation, and disaster recovery, so that incident response is consistent.
36. As a Developer, I want application code to publish typed integration events through one abstraction, so that business logic does not manage RabbitMQ connections or consumer queue names.
37. As a Developer, I want RabbitMQ transport details isolated from event contracts and handlers, so that topology, confirmation, dispatch, retry, and trace behavior are implemented once.
38. As a Developer, I want stable explicit versioned routing keys, so that renaming a CLR type does not silently break distributed contracts.
39. As a Developer, I want publishers to target facts rather than consumer queues, so that new subscribers can be added without modifying producers.
40. As a Developer, I want every outgoing event stored in the same transaction as its business state change, so that code cannot accept state while losing publication intent.
41. As a Developer, I want every consumer keyed by consumer name and immutable event ID, so that redelivery is an expected case rather than an exceptional duplicate.
42. As a Developer, I want Transcript completion to commit its inbox, Transcript revision, Consultation state, audit, and result outbox atomically, so that RabbitMQ acknowledgement has a clear durable boundary.
43. As a Developer, I want publisher confirms and mandatory-return handling, so that an outbox event is not marked published when the broker did not route it.
44. As a Developer, I want bounded delayed retries instead of immediate requeue loops, so that transient failures do not starve healthy work or flood infrastructure.
45. As a Developer, I want malformed and unsupported contracts dead-lettered immediately, so that retrying non-repairable input does not waste the retry budget.
46. As a Developer, I want valid but permanently unprocessable audio recorded as Transcription Failed rather than a poison message, so that business outcomes remain distinct from transport failures.
47. As a Developer, I want the Transcription Worker to share the Consultation Processing database and domain rules, so that completion remains one local transaction.
48. As a Developer, I want schema migrations owned by one deployment migration job, so that independently starting API and worker replicas never race database changes.
49. As a Developer, I want expand-and-contract schema compatibility during deployments, so that old and new container versions may overlap safely.
50. As a Developer, I want the worker bound only to Consultation Audio Uploaded, so that non-audio Document Processing can evolve independently.
51. As a Developer, I want the backend to consume Transcript Ready, so that it remains the sole trusted caller that supplies authorization context to Clinical Knowledge.
52. As a Developer, I want transcript text retrieved through the approved backend data boundary rather than included in events, so that broker storage remains minimal.
53. As a Developer, I want Consultation Deleted to drive idempotent subscriber cleanup, so that deletion does not require inspecting or rewriting queue history.
54. As a Developer, I want one root Compose environment, so that the complete pipeline can run and fail predictably without Azure Functions tooling.
55. As a Developer, I want the API and worker packaged and scaled independently, so that HTTP demand and speech-processing demand do not share a scaling decision.
56. As a Developer, I want Azure Functions packages, triggers, configuration, and deployment artifacts removed, so that the system has one normal container hosting model.
57. As a Developer, I want useful Blob and Azure Speech adapter behavior ported before deleting the unused Function project, so that the replacement preserves validated capabilities.
58. As a Developer, I want event serialization compatibility tests, so that producer and consumer releases can evolve deliberately.
59. As a Developer, I want a synthetic PHI canary test across logs, traces, metrics, audit, dead-letter tooling, and reports, so that forbidden content leakage fails CI.
60. As a Developer, I want architecture checks that reject Functions artifacts, legacy queue names, direct default-exchange publication, CLR-name routing, payload logging, and queue-draining deletion, so that removed patterns cannot return unnoticed.
61. As a Clinical Knowledge maintainer, I want Transcript Ready submissions to use a stable document identity and increasing revision, so that corrections supersede prior derived material.
62. As a Clinical Knowledge maintainer, I want the backend to reconcile uncertain HTTP acceptance before resubmitting, so that a lost response does not create duplicate active Documents.
63. As a Clinical Knowledge maintainer, I want deleted Consultations to trigger idempotent Un-ingest, so that derived Chunks and payloads converge with Care Workflow state.
64. As a Clinical Knowledge maintainer, I want the existing ingestion recovery and correction semantics preserved, so that the event-bus change does not redesign the AI pipeline.
65. As a Security reviewer, I want an explicit allowlist for event, outbox, inbox, log, trace, metric, and dead-letter data, so that minimization can be verified rather than assumed.
66. As a Security reviewer, I want negative authorization tests for each service identity, so that least-privilege boundaries are proven.
67. As a Security reviewer, I want TLS, protected networks, secret-store injection, credential rotation, retention, and replay authorization documented and tested, so that production controls are deployable and auditable.
68. As a Product owner, I want the unused Function replaced directly before first deployment, so that no engineering time is spent building a migration for nonexistent production state.
69. As a Product owner, I want the work accepted through externally visible outcomes rather than internal class structure, so that refactoring does not invalidate the specification.
70. As a Product owner, I want implementation to stop if evidence of deployed legacy data or durable messages appears, so that the pre-production assumption cannot cause data loss.

## Implementation Decisions

- Transcription runs as an independently deployable standard .NET Worker within the Consultation Processing context. The target contains no Azure Functions runtime, trigger extension, host configuration, or Function App deployment requirement.
- The solution adds a broker-independent event-bus module for the envelope, explicit name mapping, publication abstraction, typed handlers, and subscription registration, plus a RabbitMQ module for connection/topology, confirms, mandatory returns, dispatch, manual acknowledgement, retries, dead-lettering, and trace propagation.
- All integration events publish to one durable direct exchange named `medicalassistant.events`. Publishers know stable routing keys but never subscriber queue names. Each subscriber owns its durable main, retry, and dead-letter queues.
- Initial routing contracts are `consultation.audio-uploaded.v1`, `consultation.document-uploaded.v1`, `consultation.transcript-ready.v1`, `consultation.transcription-failed.v1`, and `consultation.deleted.v1`.
- Integration-event names are explicit wire contracts and are not derived from CLR type names. Breaking semantic or structural changes introduce a new routing-key version, with overlapping support during any future version migration.
- Every envelope carries immutable event ID, stable event type/version, UTC occurrence time, logical producer, correlation ID, causation ID, and a minimal event-specific payload. W3C trace context travels in RabbitMQ headers rather than serving as durable business identity.
- Event payloads exclude audio bytes, Transcript text, patient/doctor identifiers unless an authorized subscriber proves necessity, original filenames, credentials, signed URLs, provider error bodies, and complete entity snapshots.
- Audio Uploaded carries the Consultation/source identity, content type, opaque private storage reference, and duration when known. Document Uploaded is a separate fact and carries document-specific metadata. The Transcription Worker binds only the audio contract.
- Transcript Ready carries Consultation, Transcript, and monotonically increasing revision identity plus language when known. Initial completion and every clinician correction create distinct event IDs for the same stable Transcript/Clinical Knowledge document identity.
- Consultation state and its outgoing event commit atomically in a transactional outbox. Direct request-thread RabbitMQ publication is removed.
- A continuous outbox relay safely claims eligible records, publishes persistent mandatory messages with the event ID as message ID, requires a positive broker confirm, records safe failure state/backoff, and recovers expired claims. A crash after broker acceptance may cause duplicate publication by design.
- Consumers provide durable at-least-once processing. A consumer acknowledges only after durable handler success. Distributed exactly once across PostgreSQL, RabbitMQ, Blob Storage, Azure Speech, and Clinical Knowledge is explicitly not claimed.
- Each consumer uses an inbox unique by consumer name and event ID. A completed duplicate becomes a successful no-op; abandoned in-progress claims are recoverable.
- Transient failures receive five delayed retries with increasing deployment-configurable delays. Immediate requeue loops are prohibited. Retry exhaustion routes to the subscriber dead-letter queue, alerts operators, and requires controlled replay.
- Malformed, unsupported, or policy-invalid events dead-letter immediately. A valid Recording that reaches a permanent speech outcome commits Transcription Failed and is acknowledged as a business result rather than treated as poison transport data.
- The Transcription Worker and backend share the Consultation Processing database and application/domain persistence rules. The worker uses a restricted database role. One migration job owns schema changes; application hosts do not run migrations on startup.
- The worker completion transaction includes inbox outcome, Transcript insert/update and revision, Consultation status, content-free processing audit, and a Transcript Ready or Transcription Failed outbox event. RabbitMQ acknowledgement follows commit.
- The worker checks current deletion/revision state before costly work and again inside the completion transaction. A stale event discards temporary work, records a safe no-op where useful, and acknowledges.
- Azure Blob retrieval uses an opaque private reference and workload identity or secret-store credentials. Azure Speech uses bounded timeout/cancellation, stable transient/permanent failure classification, and no provider-body/content logging.
- Non-audio Consultation Documents do not enter the worker and never create placeholder Transcripts. They remain visibly pending Document Processing until a separate capability is implemented.
- The backend owns a durable Transcript Ready subscriber. It loads the current authorized Transcript and Doctor/Patient context, submits a Session Transcript to Clinical Knowledge over authenticated HTTP, persists the returned ingestion identity/inbox outcome, and reconciles uncertain responses idempotently.
- The existing Clinical Knowledge ingestion, recovery, correction, Un-ingest, and retrieval behavior remains authoritative. The integration must use its current ingestion contract rather than obsolete AI-module routes that coexist in older backend code.
- Consultation deletion atomically records authoritative deleted state, a content-free tombstone/cleanup intent, and Consultation Deleted in the outbox. Queue scanning/draining/republication is removed.
- Blob deletion and Clinical Knowledge Un-ingest are independently retryable/idempotent. Missing resources count as cleanup success. Pending cleanup state and age are observable.
- Normal telemetry uses an explicit allowlist: service/environment, event type/version and approved identifiers, attempt, outcome/category, sizes/counts, locale, and elapsed time. Serialized events, blob/file names and URIs, Transcript previews, exception/provider bodies, audio, and credentials are prohibited.
- RabbitMQ, database, Blob, Speech, backend, Clinical Knowledge, and operator/replay access use separate least-privilege identities. Production transport uses TLS; management/metrics endpoints remain protected; secrets come from the platform secret store.
- The API, worker, Clinical Knowledge service, RabbitMQ, PostgreSQL, and Blob development dependency run as ordinary containers. Root Compose provides the complete local/integration environment. Aspire is optional and not an architectural dependency.
- API and worker replicas scale independently. Worker concurrency/prefetch is bounded by oldest queue age, audio memory, PostgreSQL capacity, and Azure Speech quota rather than CPU or raw message count alone.
- The existing Function code has never run in a deployed environment. No bridge, dual publication, queue drain, historical backfill, or compatibility flag is built. Useful Blob/Speech behavior is ported, the new path is proven from empty infrastructure, and all Function/legacy queue artifacts are removed in the same implementation effort.
- Required schema work includes outbox records, inbox records, safe lease/retry metadata, Transcript revision/concurrency support, stable source identity where necessary, and deletion tombstone/cleanup tracking. Outbox/inbox/operational rows follow approved retention and contain no unnecessary clinical content.
- Required operational controls include dashboards/alerts for outbox age, main/retry/dead-letter queue age/depth, reconnect/ACK latency, Transcription outcomes/throttling, duplicates/stale no-ops, Clinical Knowledge handoff, deletion cleanup, and database/broker health.
- Replay is an approved operator action only after diagnosis, current-state/contract checks, and authorization. It republishes the immutable original event with the same event ID; operators do not edit payloads in the RabbitMQ management interface.

## Testing Decisions

- A good test observes behavior through the highest stable external boundary and remains valid if internal classes, namespaces, or module structure change. It asserts durable state, public/service API outcomes, event contract behavior, acknowledgement/retry/dead-letter effects, and absence of forbidden data—not private method calls.
- The primary acceptance seam is one root-Compose end-to-end harness. It submits synthetic audio through the existing backend HTTP API, runs real PostgreSQL and RabbitMQ, replaces only external Blob/Azure Speech calls with controlled adapters, and verifies Consultation status, Transcript, and Clinical Knowledge ingestion through service APIs.
- The primary harness controls infrastructure to simulate broker unavailability, relay/worker crashes before and after commit, redelivery, retry exhaustion, backend/Clinical Knowledge outages, deletion races, graceful shutdown, and scale-out. Tests observe eventual externally visible outcomes and broker states rather than implementation callbacks.
- Event serialization contract tests cover every supported routing-key version, required/optional fields, tolerant-reader behavior, unsupported versions, invalid/oversized input, explicit name mapping, and forbidden PHI/content fields. CLR refactors must not change wire names.
- PostgreSQL integration tests use a real disposable database to verify atomic outbox/business commits, atomic inbox/Transcript/status/audit/result commits, concurrent duplicate handling, relay claiming/expired lease recovery, revision conflicts, deletion/completion races, and restricted worker authorization.
- RabbitMQ integration tests use a real disposable broker to verify direct bindings, subscriber isolation, persistence, mandatory returns, publisher confirms, manual acknowledgement timing, delayed retries, dead-letter routing, duplicate publication, reconnection, and trace propagation.
- Adapter contract tests verify private Blob authorization, opaque references, content/size limits, bounded reads, cancellation, Azure Speech failure classification, protected diagnostics, Clinical Knowledge authentication, stable document identity, and uncertain-response reconciliation.
- End-to-end scenarios include happy audio processing; broker outage during upload; crashes before and after commit; five retries/DLQ/replay; permanent invalid audio; document upload isolation; deletion during speech; Transcript correction; backend/Clinical Knowledge outage; multi-replica processing; and graceful shutdown.
- PHI leakage tests inject unique synthetic canary values into filenames, paths, Transcript content, exceptions, provider bodies, and forbidden fields, then fail if they appear in normal logs, traces, metrics, audit metadata, dead-letter incident output, or test reports.
- Architecture enforcement checks reject active Azure Functions artifacts, legacy queue names, default-exchange integration publication, CLR-derived routing, serialized payload logging, Transcript previews, queue-draining deletion, and automatic application-host schema migration.
- Existing prior art is reused: backend application validator/unit tests; backend persistence integration-test infrastructure; and the Clinical Knowledge API fixture/fake-provider approach already used for duplicate submission, correction, crash recovery, retry, schema migration, Un-ingest, authentication, and Session Transcript ingestion behavior.
- Live Azure provider tests remain a separate protected pipeline/profile using synthetic non-patient fixtures; ordinary CI must not require cloud credentials or expose clinical data.
- Release requires all unit, contract, database, broker, adapter, failure-injection, security-log, and end-to-end tests; clean root Compose startup without Functions tooling; migration rehearsal on disposable infrastructure; and security/operations review of identities, alerts, retention, replay, backup, and recovery.

## Out of Scope

- Building the non-audio Document Processing service or selecting its extraction provider.
- Redesigning Clinical Knowledge chunking, embeddings, retrieval, Grounded Answer generation, or model selection.
- Replacing RabbitMQ with another broker.
- Claiming distributed exactly-once execution.
- Rebuilding every messaging workflow outside Consultation Processing.
- Adopting .NET Aspire as a required runtime or deployment platform.
- Selecting the final production container orchestrator, managed-broker product, regional topology, or high-availability tier.
- Redesigning the Doctor-facing upload/edit/delete UI beyond exposing honest existing processing outcomes.
- Defining legal retention, residency, erasure timelines, or compliance conclusions; those require privacy/compliance approval.
- Migrating live Function executions, production queues, or historical Transcript Ready messages, because the Function has never run in a deployed environment.
- Preserving legacy direct queue contracts or obsolete AI-module HTTP endpoints.
- Choosing fixed retry-delay values before dependency behavior and operational objectives are measured; the accepted policy fixes five increasing retries but leaves intervals configurable.
- Supporting operator modification of event payloads during replay.

## Further Notes

- The architecture borrows the useful structure from the official dotnet/eShop implementation: broker-independent event contracts, typed handlers, dependency-injection subscriptions, a RabbitMQ hosted consumer, durable subscriber queues, and trace propagation. It deliberately strengthens sample behavior with a continuous transactional outbox relay, publisher confirms/mandatory returns, inbox idempotency, bounded delayed retries, dead-letter/replay operations, stable versioned routing keys, authoritative deletion gates, and PHI-safe telemetry.
- The accepted domain boundaries are load-bearing: Transcription produces an editable Transcript; the backend submits that Transcript as a Clinical Knowledge Document for Ingestion; Document Processing is separate from audio Transcription; and dead-lettering is an operational condition rather than a Transcription Failed business outcome.
- The pre-production assumption is also load-bearing. If any deployed Function resource, durable legacy message, or user data is discovered, implementation must stop and replace the immediate-removal decision with a reviewed migration plan.
- The detailed event-bus documentation and accepted ADRs are the implementation specification that accompanies this PRD. If implementation reveals a conflict, update/reopen the appropriate ADR rather than silently diverging.
