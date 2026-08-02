---
status: accepted
date: 2026-08-02
---

# Share the Consultation Processing database with the Transcription Worker

The Main Backend and standalone Transcription Worker will share the Consultation Processing database and application/domain persistence model. The worker is an independently deployable process within the same bounded context, not a separately owned business service. Its event handler can therefore atomically commit the inbox result, Transcript, Consultation status, processing audit, and outgoing outbox event.

We rejected a private HTTP completion API for this stage because it adds a network failure boundary after an expensive speech call and complicates the single durable commit. We rejected giving the worker an independently modeled database because Consultation and Transcript ownership would be split without a domain reason and would require a distributed workflow merely to complete transcription.

## Consequences

- One shared persistence/application module defines consultation state transitions and the processing unit of work used by both hosts.
- The worker receives a least-privilege database credential limited to the tables and operations required for inbox, consultation/transcript completion, outbox, and audit metadata.
- A single deployment migration job owns schema upgrades. Neither the API nor the worker automatically runs migrations on startup.
- Schema changes must remain expand-and-contract compatible while old and new API/worker versions may overlap during rolling deployment.
- The worker performs a deletion/revision state check inside the same transaction that commits its result.
- Separate health, scaling, and release lifecycles remain possible even though the processes share data ownership.
- If Consultation Processing later becomes an independently owned service, moving behind an API is a deliberate future boundary change rather than an accidental partial split.
