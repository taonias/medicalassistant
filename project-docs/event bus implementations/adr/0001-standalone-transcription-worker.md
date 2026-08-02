---
status: accepted
date: 2026-08-02
---

# Replace the Azure Function with a standalone Transcription Worker

Transcription will run in an independently deployable .NET worker that subscribes to consultation integration events through RabbitMQ. We rejected hosting the consumer inside the main API because long-running speech work must scale, restart, and fail independently from clinician-facing HTTP traffic; we rejected hosting it inside Clinical Knowledge because speech-to-text belongs to Consultation Processing, not document retrieval or grounded answering.

## Consequences

- The target system contains no Azure Functions runtime or RabbitMQ-trigger extension.
- The worker owns Azure Blob retrieval, Azure Speech transcription, transcript persistence, consultation-status transitions, processing audit, and publication of `Transcript Ready`.
- RabbitMQ consumption becomes an application-hosted background service using shared event-bus abstractions.
- The worker is independently containerizable and deployable, but it adds a service that must have health checks, configuration, observability, and lifecycle orchestration.
- The existing `transcriber` project is removed only after the worker passes contract and cutover tests.

