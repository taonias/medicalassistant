---
status: accepted
date: 2026-08-02
---

# Use explicit versioned integration-event contracts

All services will publish integration events to the direct exchange `medicalassistant.events` using stable, explicitly assigned routing keys. Publishers know event contracts but do not know subscriber queue names. Each independently deployable subscriber owns its durable queue and binds only the routing keys it consumes.

The initial Consultation Processing contracts are (audio/document separation was accepted in ADR 0008):

- `consultation.audio-uploaded.v1`
- `consultation.document-uploaded.v1`
- `consultation.transcript-ready.v1`
- `consultation.transcription-failed.v1`
- `consultation.deleted.v1`

We rejected using CLR type names as routing keys because a class rename would silently become a distributed wire-contract change. We also rejected publishing directly to named consumer queues because it couples producers to the current set of consumers and prevents independent subscriptions.

## Consequences

- Code maps each integration-event type to an explicit routing key; reflection-based `typeof(T).Name` naming is not part of the wire protocol.
- Compatible additions may extend a versioned payload only when old consumers safely ignore them. Breaking semantic or structural changes introduce a new versioned routing key.
- Old and new versions coexist during a measured migration window; producers stop the old version only after subscriber adoption is verified.
- Events contain identifiers and the minimum metadata necessary for subscribers. They do not contain audio bytes, transcript text, credentials, signed URLs, filenames containing patient information, or complete clinical records.
- Trace propagation travels in RabbitMQ headers. Event ID, correlation ID, and causation ID remain durable envelope metadata.
- Queue, retry, and dead-letter names are subscriber deployment details and may change without changing producer contracts.
