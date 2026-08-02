---
status: accepted
date: 2026-08-02
---

# Replace the unused Azure Function directly before first deployment

The Azure Function transcriber has never run in a deployed environment. The implementation will therefore replace it directly with the standalone Transcription Worker and new event-bus path before first deployment. There is no production queue backlog, processed history, deployed Function resource, or compatibility consumer to migrate.

We rejected a rolling legacy drain because it protects state that does not exist and would temporarily preserve the exact Functions dependency the target removes. We also rejected dual publication because it creates duplicate-processing risk without providing migration value.

## Consequences

- No legacy queue bridge, backfill, shadow consumer, dual publishing, or coexistence feature flag is required.
- Useful speech, blob, and persistence logic is ported/refactored into the new worker before the `transcriber` project is deleted; removal does not mean discarding validated behavior.
- New database tables/columns, event contracts, exchange, queues, and Compose orchestration form the only supported initial deployment state.
- Functions packages, trigger attributes, `host.json`, Functions settings, Function-specific documentation, and legacy direct-queue configuration are removed in the implementation change.
- Tests start with empty infrastructure and prove the complete new path before any environment is declared deployable.
- If evidence of a deployed Function, durable messages, or user data is discovered, this assumption is invalid and removal pauses for a migration review.
