# Persistence characterization tests

This project contains two kinds of tests:

- Existing in-memory tests provide fast feedback for persistence-facing modules.
- `PostgreSql*CharacterizationTests` protect behavior that only the production database adapter can prove.

The PostgreSQL tests start a disposable `pgvector/pgvector:pg16` container, matching the PostgreSQL major version used by the root Compose files. The database is recreated and migrated before each test. Docker must be available.

Run only the real-database gate from the repository root:

```powershell
dotnet test backend/test/MedicalAssistant.Persistence.IntegrationTests/MedicalAssistant.Persistence.IntegrationTests.csproj --filter "FullyQualifiedName~PostgreSql"
```

## Protected behavior

| Test area | Observable invariant |
|---|---|
| Recording/outbox | Recording registration and Consultation Audio Uploaded persistence commit or roll back together. |
| Durable messaging | Inbox identity is unique per consumer/event, and simultaneous outbox relay owners receive disjoint leases. |
| Transcription completion | Inbox, Transcript, Consultation status, and Transcript Ready fact become durable together. |
| Transcript revision | Reprocessing advances the revision/concurrency identity and publishes the same revision. |
| Deletion race | The deterministic K02 test records the current race in which Transcript Ready acceptance can reactivate a tombstoned Consultation. |

The K02 test is a characterization of a known defect, not approval of that behavior. Fix K02 in a separate behavior-change commit and change the test expectation with that fix. See `docs/known-issues/refactor-baseline.md`.

## Current limitation

The fixture proves behavior on an empty database migrated to the current schema. Several tests seed existing rows, but this gate does not yet attach to a representative existing volume or prove an upgrade from an older migration. Data Steward + Quality must add that environment-level validation before the Wave 1 exit gate and before the R14/R16 backend and persistence moves.
