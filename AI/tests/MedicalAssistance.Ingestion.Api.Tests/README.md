# Clinical Knowledge characterization tests

This project hosts the whole `MedicalAssistance.Ingestion.Api` service
in-process against a real `pgvector/pgvector:pg17` container (`IngestionApiFixture`).
Tests cross only the public HTTP/SignalR interface; the AI seams (chat,
embeddings) carry fakes. Run everything from the repository root:

```powershell
dotnet test AI/tests/MedicalAssistance.Ingestion.Api.Tests/MedicalAssistance.Ingestion.Api.Tests.csproj
```

Docker must be available.

## Protected behavior

| Test area | Covered by | Observable invariant |
|---|---|---|
| Ingestion request/result contract | `RequestValidationTests`, `ChunkPlanValidationTests` | Rejected submissions name every missing/invalid field; accepted ones return the expected shape. |
| Document identity/supersession | `DuplicateSubmissionTests`, `CorrectionTests` | Re-posting identical content is a no-op; a correction supersedes the prior ingestion without touching the rest of the session. |
| Recovery | `CrashRecoveryTests`, `ConcurrentStartupTests` | Abandoned work is picked up by the next startup or sweep, exactly once, with a bounded attempt cap. |
| Retrieval citations/refusal | `RefusalAndThresholdTests`, `CitationVerificationTests` | Below-threshold evidence refuses rather than answers; every answer carries a citation. |
| Outbox / callback envelope | `IntegrationEventOutboxCharacterizationTests` | A terminal Session Transcript failure writes one `clinicalknowledge.ingestion-failed.v1` outbox row, in the same transaction as the `Failed` status, in the exact envelope shape the backend's `IngestionFailedContractTests` pins from its side. A successful ingestion writes none. |

## Current limitation

`IntegrationEventOutboxCharacterizationTests` proves the outbox row's shape
and transactional durability, and that `IntegrationEventOutboxRelay` stays
inert when no broker is configured (the fixture's current state). It does
**not** exercise:

- Outbox scoping for document types other than `SessionTranscript` — a failed
  `LabReport`, `ImagingReport`, or `DoctorNote` is not proven to write (or not
  write) a row either way; `IngestionStore.MarkFailedAsync` only writes one
  for `SessionTranscript`, but that branch itself is untested for the other
  three types.
- The relay's real-broker path: publish-then-mark-published, retry backoff,
  and the single-drain advisory lock across concurrent instances. No test in
  this repository uses `Testcontainers.RabbitMq`; the full-system-acceptance
  suite (R06) exercises a real broker only for the happy Recording→Transcript
  path, not for this event.

Both are open follow-up work, not confirmed defects. See
`docs/known-issues/refactor-baseline.md` for the separate defect ledger.
