# Durable Messaging

## Purpose

Owns the wire mechanics of getting a fact from one bounded context to another durably: envelopes, routing/topology, delivery, ACK/DLQ, and the inbox/outbox pattern every publisher and subscriber uses. Knows nothing about what a `consultation.transcript-ready.v1` event *means* clinically — only that it must arrive at least once and never be silently lost.

## Public interface / seam

Versioned integration contracts (ADR-0004): [backend/src/MedicalAssistant.ConsultationProcessing.Contracts/](../../backend/src/MedicalAssistant.ConsultationProcessing.Contracts/). Transport: [backend/src/MedicalAssistant.EventBus/](../../backend/src/MedicalAssistant.EventBus/) (abstractions) and [backend/src/MedicalAssistant.EventBusRabbitMQ/](../../backend/src/MedicalAssistant.EventBusRabbitMQ/) (the RabbitMQ adapter). Persistence-side inbox/outbox/replay: `Modules/ConsultationProcessing/DurableMessaging/` under both [Application](../../backend/src/MedicalAssistant.Application/Modules/ConsultationProcessing/DurableMessaging/) and [Persistence](../../backend/src/MedicalAssistant.Persistence/Modules/ConsultationProcessing/DurableMessaging/) (`DurableMessagingPersistenceRegistration.cs`, R38).

## Invariants

- At-least-once delivery with idempotent handlers (ADR-0003): every consumer keeps an inbox keyed by integration-event ID so a redelivery is recognized and acknowledged without repeating completed work.
- A transactional outbox (ADR-0002): every outgoing event is written in the same database transaction as the state change it announces — an unavailable broker never orphans an accepted fact.
- Explicit versioned routing keys (ADR-0004), never CLR type names — a class rename must never become a silent wire-contract change.
- Payloads carry identifiers and minimum metadata only — never audio, transcript text, credentials, or signed URLs (see [messaging-and-recovery.md](../runbooks/messaging-and-recovery.md)'s "Minimum dashboard" and "Prohibited operator actions").

## Dependencies

**Owns**: envelopes, routing/topology, delivery, ACK/DLQ, inbox/outbox mechanics.
**Does not own**: the clinical meaning of any event — that belongs to whichever context publishes or consumes it (Consultation Lifecycle, Consultation Processing, Clinical Knowledge).

## Tests

`backend/test/MedicalAssistant.EventBus.UnitTests`, `backend/test/MedicalAssistant.EventBusRabbitMQ.UnitTests` — wire/broker/crash/replay tests; `backend/test/MedicalAssistant.Persistence.IntegrationTests/Modules/ConsultationProcessing/DurableMessaging/` — real PostgreSQL characterization of the inbox/outbox/completion-unit-of-work paths.

## Runbook & known risks

Runbook: [docs/runbooks/messaging-and-recovery.md](../runbooks/messaging-and-recovery.md) — RabbitMQ outages, outbox/DLQ replay, worker crash loops, disaster recovery, prohibited operator actions.

Known risks tracked in [docs/known-issues/refactor-baseline.md](../known-issues/refactor-baseline.md):

| ID | Finding |
|---|---|
| K04 | Outbox leasing is PostgreSQL-specific while SQL Server remains advertised. |
| K05 | RabbitMQ consumer handling drops cancellation in parts of the delivery path. |
| K07 | ADR retry policy says five delayed retries while runtime RetryDelays is empty. |
| K30 | Clinical Knowledge can mark an unroutable RabbitMQ event published. |
| K31 | A crash after Clinical Knowledge returns 202 can redeliver into a legitimate 409 without reconciliation. |
