---
status: accepted
date: 2026-08-02
---

# Process integration events at least once with idempotency and dead-lettering

Event consumers will use at-least-once delivery. A handler acknowledges a RabbitMQ delivery only after its durable work has committed. Each consumer keeps an inbox record keyed by the integration-event ID so a redelivered event can be recognized and acknowledged without repeating completed work. We rejected acknowledging failed deliveries, as the eShop sample does, because that can silently discard clinical processing; we also rejected attempting distributed exactly-once delivery because RabbitMQ, the database, Blob Storage, and Azure Speech do not share one transaction.

Transient failures receive up to five delayed retries with increasing backoff. After the retry budget is exhausted, RabbitMQ routes the delivery to the subscriber's dead-letter queue, an alert is raised, and replay requires an explicit operator action. Retry intervals are configurable deployment policy rather than part of the event contract.

## Failure classification

- **Duplicate delivery**: if the inbox says the event was already completed, perform no business work and acknowledge it.
- **Transient dependency failure**: do not acknowledge; route through the delayed-retry topology while attempts remain.
- **Valid but unprocessable Consultation File**: atomically record the terminal consultation outcome and an outgoing `Transcription Failed` event, then acknowledge. This is a business outcome, not a poison message.
- **Malformed, unsupported, or policy-invalid event**: dead-letter immediately because repeating it cannot repair the contract.
- **Unhandled failure after five retries**: dead-letter, alert, investigate, and replay only after the cause is understood.

## Consequences

- The worker transaction commits the inbox result, consultation/transcript changes, processing audit, and outgoing outbox events before acknowledging the delivery.
- Crashes before acknowledgement can cause redelivery but cannot intentionally repeat completed transcription-side effects.
- External calls such as Azure Speech may still be repeated after a crash; their results must be applied idempotently and temporary artifacts must be reconcilable.
- Every subscriber owns a durable main queue, retry topology, and dead-letter queue.
- Dead-letter depth, oldest-message age, retry counts, failure classifications, and replay outcomes become required operational metrics.
- Logs and traces identify an event by type, ID, correlation ID, consultation ID, attempt, duration, and outcome. They never contain audio, transcript text, credentials, signed blob URLs, or complete event payloads.
