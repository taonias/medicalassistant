# Operations and Recovery Runbook

## Operating principle

The database records accepted business intent and results; RabbitMQ delivers work. Diagnose both before replaying or changing state. Queue depth alone does not prove loss, and manually deleting messages does not correct business state.

## Minimum dashboard

- Outbox pending count, oldest age, publish rate/failures/attempts, expired leases
- Main/retry/DLQ depth and oldest age for each subscriber
- Consumer connected/readiness state, delivery/ACK latency, reconnects
- Transcription throughput, duration percentiles, stable outcome categories, Azure Speech throttling/timeouts
- Duplicate and stale-event no-op counts
- Transcript Ready → Clinical Knowledge handoff latency/reconciliation failures
- Deletion cleanup pending/oldest age/failures
- PostgreSQL connection/transaction/lock health and RabbitMQ disk/memory alarms

Every panel links through safe identifiers to protected diagnostic views; it never displays transcript/audio content.

## RabbitMQ unavailable

1. Confirm broker/network/TLS/credential state and whether outage is planned.
2. Verify uploads continue committing outbox records; monitor database capacity and oldest outbox age.
3. Do not ask clients to re-upload solely because RabbitMQ is unavailable.
4. Restore broker service and validate exchange/bindings/permissions without deleting durable queues.
5. Confirm relays reconnect and outbox lag decreases.
6. Watch consumer queue age, provider quotas, and database load while backlog drains; scale conservatively.
7. Reconcile old outbox records and subscriber outcomes before closing.

## Outbox is not publishing

1. Check relay readiness, lease ownership/expiry, database locks, RabbitMQ confirms/returns, and routing-key bindings.
2. Distinguish unroutable contract/topology errors from transient connection failures.
3. Repair topology/configuration or relay defect; do not mark records published manually.
4. Let expired leases recover or use an audited administrative release action.
5. Confirm the same event IDs publish and consumer inboxes converge duplicates safely.

## Transcription backlog or Azure Speech outage

1. Check queue oldest age, active/retry distribution, worker readiness, concurrency, resource use, and Speech quota/status.
2. If Speech is broadly unavailable/throttled, reduce/pause new consumption while allowing durable queues/outbox to retain work; avoid burning all retries rapidly.
3. Do not increase replicas above provider/database limits.
4. After recovery, resume gradually and watch latency/error rates.
5. Replay DLQ messages only if they exhausted retries during the confirmed outage and current Consultation state remains eligible.

## Dead-letter investigation and replay

1. Open an incident with event ID/type, subscriber, Consultation ID, attempt count, safe failure category, and timestamps.
2. Inspect protected payload only if authorized and necessary; never paste it into tickets/chat/logs.
3. Verify schema support, current Consultation deletion/revision, resource existence, and dependency repair.
4. Determine whether replay is safe. A permanent business failure is not replayed unless the underlying source is corrected through the product workflow.
5. Use the approved replay command/tool with operator identity and reason. Preserve the original event ID/payload; do not edit it in RabbitMQ UI.
6. Observe main queue → inbox/business state → outgoing events. Confirm duplicates/stale events no-op as expected.
7. Record resolution and close only after state reconciliation.

Implemented replay policy controls require:

- the immutable original integration event ID;
- a non-empty operator identity;
- a non-empty operational reason code;
- no replacement payload JSON or manually edited message body.

The implemented replay service now provides the approved application seam for tooling:

1. Parse a dead-lettered integration-event envelope and return only safe metadata: event ID, event type/version, subscriber, attempt count, relevant Consultation ID, correlation/causation IDs, safe failure category, and dead-letter timestamp.
2. Reject malformed envelopes before publication.
3. Apply the replay policy above, including the prohibition on replacement payload JSON.
4. Run replay safety checks before publication. The first implemented safety check rejects unsupported event contract type/version; additional checks can verify current Consultation deletion/revision/resource state before replay.
5. Republish the original immutable envelope body through RabbitMQ publisher confirms using the original event ID as the message ID and the original event type as the routing key.

The policy emits an audit action/details string that contains the operator, original event ID, and reason code, but never copies payload content. Replay result metadata likewise avoids clinical payload fields such as blob paths, transcript text, provider bodies, filenames, or exception text. `EventRetention` configuration keeps outbox, inbox, DLQ, and tombstone periods explicit; tombstones must outlive all event-record retention windows so late delivery cannot restore deleted content after cleanup.

## Poison or unsupported contract

1. Keep the message in DLQ; stop repeated manual replay.
2. Identify producer/version and compare with committed contract tests.
3. If producer is wrong, fix it and create a correct new business event through an approved repair process.
4. If consumer support is missing, deploy compatible support, then replay the immutable original.
5. Look for additional messages from the same producer/version and contain publication if necessary.

## Deletion cleanup not converging

1. Verify the Consultation tombstone/deleted state first; this protects against stale processing.
2. Inspect per-resource cleanup status for Blob and Clinical Knowledge.
3. Treat already-missing resources as successful idempotent cleanup.
4. Repair authorization/dependency issues and replay only the failed cleanup step/event.
5. Confirm no active Transcript/knowledge document remains and required audit/tombstone policy is satisfied.
6. Escalate based on approved privacy incident thresholds and timelines.

## Worker crash loop

1. Stop automatic restart escalation if it is causing dependency pressure; retain unacknowledged work in RabbitMQ.
2. Check configuration/schema compatibility, startup logs with safe categories, broker/database credentials, and latest deployment.
3. Identify whether one poison delivery or all startup attempts cause the crash.
4. For a poison message, ensure it dead-letters according to policy rather than crashing the host indefinitely.
5. Roll forward/fix or use the approved deployment rollback while preserving database expand-contract compatibility.
6. Confirm readiness, consumer connection, and backlog drain after recovery.

## Credential rotation

1. Provision a new credential/identity permission without removing the old one.
2. Update one workload/configuration at a time and verify reconnect/readiness/authorization.
3. Observe publication/consumption/database/storage/Speech/Clinical Knowledge success.
4. Revoke the old credential and verify it no longer works through a safe negative check.
5. Record rotation and investigate any unexpected continued use.

## Capacity and backpressure

- Scale from oldest-message age and measured service latency, not queue count alone.
- Bound RabbitMQ prefetch, worker parallelism, audio memory, HTTP connections, database pool, and Speech concurrency.
- Prefer durable backlog over overload that causes timeouts/retries and amplifies traffic.
- Apply admission/file-size/duration limits at upload and validate again in the worker.
- Use disk/memory watermark alerts before RabbitMQ enters flow control or blocks publishers.

## Disaster recovery checks

After broker/database restore or regional recovery:

1. Validate database schema and authoritative Consultation/outbox/inbox/tombstone state.
2. Restore broker definitions/policies/permissions; do not assume queue backups represent current business truth.
3. Start relays/consumers gradually.
4. Let unpublished outbox records repopulate missing deliveries.
5. Reconcile completed Transcripts, Clinical Knowledge ingestion identities, deletion cleanup, outbox publication, inbox results, and DLQs.
6. Accept duplicate deliveries as normal and verify idempotent convergence.

## Prohibited operator actions

- Draining and rewriting a queue to remove one Consultation
- Editing message JSON in RabbitMQ UI and replaying it as the same event
- Marking outbox/inbox records successful without business-state reconciliation
- Copying audio/transcript/payload/provider bodies into tickets, chat, or normal logs
- Purging a DLQ or durable queue without written scope, reconciliation plan, and approval
- Re-enabling the removed Azure Function or legacy direct queues as an emergency workaround

## See also

- [End-to-end failure matrix](../../project-docs/event%20bus%20implementations/12-end-to-end-failure-matrix.md) — maps each scenario above to its automated test evidence and live Compose gate.
