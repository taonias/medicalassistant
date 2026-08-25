---
status: accepted
date: 2026-08-02
---

# Handle consultation deletion through state and integration events

Deleting a Consultation will atomically transition it to a deleted state, remove or schedule removal of its clinical content according to policy, retain a content-free tombstone/audit record, and add `consultation.deleted.v1` to the outbox. Subscribers perform their own idempotent cleanup. Consumers check authoritative consultation state before expensive work and before committing any result, so a stale upload or transcript-ready delivery becomes a successful no-op.

We rejected scanning, draining, or rewriting RabbitMQ queues to find messages for one Consultation. That approach races active consumers, couples the backend to every queue, loses queue order and delivery metadata, and becomes impossible once subscribers own independent queues. We also rejected relying only on message order because retries and independently published events can arrive out of order.

## Consequences

- The existing `RemovePendingForConsultationAsync` queue-drain behavior is removed during migration.
- Deletion is considered durably accepted when the database state and deletion outbox record commit; downstream cleanup is eventually consistent and observable.
- The Transcription Worker checks deletion state before blob download, before Azure Speech when practical, and immediately before committing a Transcript.
- The backend's Transcript Ready handler checks deletion state before submitting to Clinical Knowledge.
- Blob cleanup and Clinical Knowledge un-ingestion are independently retryable and idempotent.
- A content-free tombstone stores only the identifiers and policy/audit metadata needed to reject stale work. It never stores transcript text, filenames, signed URLs, or audio.
- Legal erasure and retention periods remain explicit product/compliance policy. When erasure requires removal of an identifier, a non-reversible keyed fingerprint or equivalent deny marker may replace the direct identifier if approved by privacy review.
