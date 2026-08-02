# Deletion and Stale-Event Handling

## Objective

Deleting a Consultation removes its usable clinical content without trying to edit broker history. Durable state determines whether work is still valid; messages are delivery attempts, not the source of truth.

## Deletion flow

1. The backend authorizes the deletion.
2. One database transaction marks the Consultation deleted, records audit metadata, creates a content-free tombstone, and writes `consultation.deleted.v1` to the outbox.
3. The response reports deletion accepted; downstream cleanup may still be running.
4. The outbox relay publishes the deletion fact with confirms.
5. Authorized cleanup handlers delete the private Blob Storage object and un-ingest the Clinical Knowledge document.
6. Each handler records completion idempotently. Operators can see and replay failed cleanup.

Blob deletion may begin in the request path as an optimization, but durable cleanup status and retries must not depend on that attempt succeeding.

## Consumer state gates

### Audio Uploaded handler

The Transcription Worker checks that the Consultation exists in a processable state before downloading audio. Because deletion can race a long Azure Speech call, it checks again before committing. If the Consultation is deleted, it discards temporary results, marks the incoming event handled as a no-op, and acknowledges it.

### Transcript Ready handler

The main backend checks the current Consultation and transcript revision before Clinical Knowledge submission. A deleted Consultation or superseded revision is an acknowledged no-op. This prevents a delayed event from restoring obsolete content.

### Deleted handler

Each cleanup handler keys its inbox by event ID and its cleanup state by Consultation/resource identity. Missing blobs and already-un-ingested documents count as success. Retrying deletion therefore converges instead of failing on `not found` responses.

## Ordering rule

No correctness rule depends on RabbitMQ delivering Audio Uploaded, Transcript Ready, and Deleted in wall-clock order. Event metadata helps diagnosis, but the latest authoritative state and revision decide whether an action is allowed.

## Tombstone boundary

The tombstone contains no clinical content. It records only what is necessary to reject stale work and prove policy execution, such as the Consultation identifier, deletion time, policy/reason code, and cleanup completion markers. The approved retention/erasure policy determines how long even this minimal record remains.

## Required operational signals

- Pending cleanup count and oldest age
- Blob cleanup failures
- Clinical Knowledge un-ingestion failures
- Stale Audio Uploaded and Transcript Ready no-op counts
- Deletion events in retry or dead-letter queues
- Tombstones whose required cleanup steps have not converged
