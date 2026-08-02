# Event Bus Implementation Design

This documentation defines the replacement for the Azure Functions transcriber. The target is an eShop-inspired RabbitMQ integration-event architecture with a standalone .NET Transcription Worker.

## Product value

For doctors and patients, the change is intentionally invisible when healthy: an uploaded consultation recording reliably becomes an editable Transcript and then searchable clinical knowledge. Its value appears when infrastructure fails—the upload remains accepted and recoverable instead of being silently stranded, duplicate deliveries do not duplicate clinical work, deletion cannot be undone by a stale message, and operators can identify/replay failures without exposing transcript content.

For the engineering team, it removes Azure Functions as a second hosting/deployment model, centralizes RabbitMQ behavior behind shared modules, gives every event a durable lifecycle, and makes the API, worker, and Clinical Knowledge boundary independently understandable and testable.

## Accepted direction

- Azure Functions will not be part of the target system.
- Transcription will run in an independently deployable .NET worker.
- Services will publish and subscribe through shared integration-event abstractions.
- RabbitMQ remains the broker.
- Consultation state and its outgoing processing event will be committed together through a transactional outbox.
- The design will adapt the official `dotnet/eShop` structure, but it will not copy sample behavior that is unsafe for clinical data or production delivery.

## Documents

- [Current code and gap analysis](00-current-code-and-gaps.md)
- [eShop reference analysis](01-eshop-reference-analysis.md)
- [Target event topology and contracts](02-event-topology-and-contracts.md)
- [Target architecture and end-to-end flow](03-target-architecture.md)
- [Deletion and stale-event handling](04-deletion-and-stale-events.md)
- [Standalone Transcription Worker design](05-transcription-worker-design.md)
- [Deployment and operations model](06-deployment-and-operations.md)
- [Immediate replacement and implementation plan](07-immediate-replacement-plan.md)
- [Delivery reliability specification](08-delivery-reliability.md)
- [Testing and verification strategy](09-testing-and-verification.md)
- [Security, privacy, and clinical-data controls](10-security-and-privacy.md)
- [Operations and recovery runbook](11-operations-runbook.md)
- [Consultation Processing glossary](CONTEXT.md)
- [ADR 0001: standalone Transcription Worker](adr/0001-standalone-transcription-worker.md)
- [ADR 0002: transactional outbox](adr/0002-transactional-outbox.md)
- [ADR 0003: at-least-once delivery and failure handling](adr/0003-at-least-once-delivery.md)
- [ADR 0004: explicit versioned event contracts](adr/0004-versioned-event-contracts.md)
- [ADR 0005: backend consumes Transcript Ready](adr/0005-backend-consumes-transcript-ready.md)
- [ADR 0006: event-driven deletion](adr/0006-event-driven-deletion.md)
- [ADR 0007: shared Consultation Processing database](adr/0007-shared-consultation-database.md)
- [ADR 0008: separate audio and document processing](adr/0008-separate-audio-and-document-events.md)
- [ADR 0009: container-first deployment](adr/0009-container-first-deployment.md)
- [ADR 0010: immediate pre-production Function removal](adr/0010-immediate-function-removal.md)

The documents form one implementation specification. ADRs record the accepted high-cost decisions; numbered documents explain how to build, test, deploy, secure, and operate them.

## Decision status

| Decision | Status |
| --- | --- |
| Replace Azure Functions with a standalone .NET Transcription Worker | Accepted |
| Publisher atomicity/outbox behavior | Accepted: transactional outbox and continuous relay |
| Consumer retry, acknowledgement, and dead-letter policy | Accepted: idempotent at-least-once processing, five delayed retries, then dead-letter |
| Event topology and naming/versioning | Accepted: direct exchange, subscriber-owned queues, explicit versioned routing keys |
| Transcript-ready consumer and AI integration boundary | Accepted: main backend consumes and calls Clinical Knowledge API |
| Consultation deletion/cancellation behavior | Accepted: deletion event, authoritative state gates, idempotent cleanup, no queue scanning |
| Worker/backend data boundary | Accepted: shared Consultation Processing database with restricted worker access |
| Non-audio document handling | Accepted: separate upload event and Document Processing pipeline; no placeholder Transcript |
| Deployment/orchestration model | Accepted: normal containers, root Compose locally, independent production scaling, Aspire optional |
| Migration and Azure Function cutover | Accepted: unused pre-production Function is replaced directly; no legacy coexistence or data migration |
