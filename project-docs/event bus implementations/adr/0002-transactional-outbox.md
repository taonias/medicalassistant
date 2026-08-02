---
status: accepted
date: 2026-08-02
---

# Publish consultation events through a transactional outbox

The main backend will store each outgoing consultation integration event in an outbox record within the same database transaction as the corresponding consultation-state change. A continuously running relay will claim unpublished records, publish them to RabbitMQ, and mark them published only after positive broker confirmation. We rejected direct best-effort publication from the HTTP request because a broker outage can currently leave an accepted Consultation File with no durable processing event.

Azure Blob Storage cannot participate in the database transaction. The upload flow will therefore store the blob first, then atomically commit the consultation record and outbox event. If that database transaction fails, the backend will attempt compensating deletion of the unreferenced blob, backed by periodic orphan cleanup.

## Consequences

- An unavailable RabbitMQ broker no longer causes a stored Consultation File to be silently orphaned.
- The HTTP request succeeds after the database has durably accepted both the consultation change and the intent to publish; it does not wait for transcription.
- The outbox relay must support safe concurrent claiming, retry with backoff, publisher confirms, and operational visibility for old or repeatedly failing records.
- Duplicate publication remains possible if the relay crashes after RabbitMQ accepts a message but before the record is marked published. Consumers must therefore be idempotent.
- Outbox records retain event metadata and payloads according to a defined privacy and retention policy.
- Blob/database consistency requires compensation and reconciliation because it cannot be made atomic through the outbox.
