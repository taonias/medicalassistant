# Delivery Reliability Specification

## Guarantee

The system provides durable at-least-once event delivery with idempotent business effects. It does not claim distributed exactly-once execution across PostgreSQL, RabbitMQ, Blob Storage, Azure Speech, and Clinical Knowledge.

| Boundary | Guarantee |
| --- | --- |
| Business transaction → outbox | Atomic in one PostgreSQL transaction |
| Outbox → RabbitMQ | Retried until confirmed; duplicates possible around acknowledgement uncertainty |
| RabbitMQ → consumer | Manual acknowledgement after durable handler success; redelivery possible |
| Consumer → database result/outbox | Inbox, state change, audit, and outgoing event commit atomically |
| Azure Speech call | May repeat after crash; result application is idempotent |
| Backend → Clinical Knowledge HTTP | Idempotent document identity plus reconciliation after uncertain response |

## Outbox model

Each outbox record contains:

- immutable event ID, stable event type/version, occurrence time, producer, correlation ID, and causation ID;
- serialized minimal payload;
- `Pending`, leased/in-progress, published, or failed-operational state;
- attempt count, next-attempt time, lease owner/expiry, creation time, published time;
- last stable failure category/code, never a raw provider/broker response containing sensitive data.

### Relay algorithm

1. Claim a bounded batch of eligible records using a database-safe skip-locked/lease pattern.
2. Publish each record to `medicalassistant.events` using its stable routing key, persistent delivery mode, mandatory routing, event ID as RabbitMQ message ID, and trace headers.
3. Require a positive publisher confirm and treat a returned/unroutable message as failure.
4. Mark published only after the confirm. On failure, increment attempts, schedule backoff, release/expire the lease, and alert when age/attempt thresholds are exceeded.
5. Periodically recover expired leases left by crashed relay instances.

The relay may publish an event successfully and crash before marking it published; republication is expected and handled by consumer inboxes.

## Inbox model

Inbox uniqueness is `(consumerName, eventId)`, not event type alone. The consumer transaction:

1. Looks up/inserts the inbox claim.
2. Treats an already completed entry as a successful duplicate no-op.
3. Revalidates current Consultation state/revision.
4. Applies business state, processing audit, and outgoing outbox changes.
5. Marks the inbox completed and commits.
6. Only after commit tells the event bus to acknowledge the RabbitMQ delivery.

An abandoned in-progress inbox attempt must be recoverable; it cannot permanently lock the event after a process crash.

## Acknowledgement and failure matrix

| Outcome | Database action | RabbitMQ action |
| --- | --- | --- |
| Completed normally | Commit inbox + business state + outbox | ACK |
| Already completed duplicate | No business mutation | ACK |
| Deleted/superseded stale event | Commit/record safe no-op where useful | ACK |
| Valid permanent transcription failure | Commit Failed state + failure event | ACK |
| Transient dependency/timeout | Roll back uncommitted handler work | Route to delayed retry; no success ACK |
| Malformed/unsupported/policy-invalid contract | No clinical mutation; record safe diagnostic | Dead-letter immediately |
| Unhandled failure after retry budget | Roll back uncommitted work | Dead-letter and alert |

## Five-retry topology

Each subscriber owns a main queue, five delayed retry stages, and a final dead-letter queue. Delays increase and are deployment-configurable; changing delay values does not change event contracts. A practical initial schedule may be selected from observed dependency behavior, then tuned without code changes.

Every transfer preserves original event ID/type/correlation/causation metadata and records the attempt in broker headers. The consumer does not trust arbitrary publisher-supplied attempt counts; broker topology/death headers and subscriber policy determine exhaustion.

Avoid immediate `NACK requeue=true` loops: they consume CPU, flood logs, starve healthy messages, and do not provide meaningful backoff.

## Dead-letter operations

A dead-lettered message is not silently complete. It creates an operator-visible incident/work item with event ID, event type, subscriber, first/last failure time, attempt count, safe category, and relevant Consultation ID. Payload content remains protected and is not copied into tickets or ordinary logs.

Replay requires:

1. Diagnose and repair the cause.
2. Verify the event contract remains supported and the Consultation is not deleted/superseded.
3. Record operator/reason/change reference.
4. Republish the original immutable event with the same event ID through an approved replay tool.
5. Observe inbox outcome and resulting events.
6. Close the incident only after business-state reconciliation.

Operators never edit clinical payloads in the RabbitMQ management UI. Corrected business data produces a new business operation/event; replay preserves the original fact.

Implemented replay boundary:

- `IntegrationEventReplayService` inspects a DLQ envelope into safe metadata and coordinates policy, safety checks, and publication.
- `SupportedContractReplaySafetyCheck` blocks replay of event type/version combinations the deployed service no longer supports.
- `IIntegrationEventReplaySafetyCheck` is the extension point for current Consultation deletion/revision/resource checks before an operator tool republishes the immutable event.
- `RabbitMqIntegrationEventReplayPublisher` republishes the unchanged envelope body using publisher confirms; the replay path does not expose or edit payload JSON.

## Failure scenarios

### RabbitMQ unavailable during upload

Upload succeeds after the Consultation and outbox commit. The relay accumulates lag and publishes after recovery. Alert on oldest unpublished age.

### Worker crashes during Azure Speech

The message remains unacknowledged and is redelivered. The next handler checks inbox/current state. Azure Speech may run again if no durable result was committed.

### Worker commits then crashes before ACK

RabbitMQ redelivers. Inbox says completed, so the handler performs no clinical work and acknowledges.

### RabbitMQ confirms publish but relay loses response

The outbox record may be published again. Subscriber inbox prevents duplicate business effects.

### Clinical Knowledge accepts but backend loses HTTP response

The handler reconciles using stable document/ingestion identity before resubmitting. The Clinical Knowledge correction/dedup contract prevents duplicate active documents.

### Event arrives after deletion

Authoritative tombstone/state makes it a no-op. Cleanup remains driven by the deletion event and tracking state.

## Differences from eShop sample behavior

Medical Assistant keeps eShop's modular event abstraction, hosted RabbitMQ consumer, durable subscriber queues, typed handlers, retries for connectivity, and trace propagation. It deliberately strengthens the sample by requiring publisher confirms/returns, a continuous outbox relay, inbox idempotency, delayed retry/DLQ/replay, stable explicit routing keys, authoritative deletion gates, and PHI-safe telemetry. Consumer exceptions are never caught and then blindly acknowledged.
