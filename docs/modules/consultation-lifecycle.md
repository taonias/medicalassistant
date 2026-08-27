# Consultation Lifecycle

## Purpose

Owns a Consultation's whole lifecycle for its owning Doctor: creation (with idempotency), patient assignment, file upload (audio/document), status/retry, and deletion. The doctor-facing surface for "the thing a doctor is recording, uploading, or reviewing."

## Public interface / seam

Nine narrow capability ports (R28), each protecting one specific state transition, all still backed by `ConsultationRepository` — [backend/src/MedicalAssistant.Persistence/Modules/CareWorkflow/Consultations/ConsultationRepository.cs](../../backend/src/MedicalAssistant.Persistence/Modules/CareWorkflow/Consultations/ConsultationRepository.cs):

| Port | Protects |
|---|---|
| `IConsultationAccess` | the doctor-scoped read every other use case authorizes against |
| `IConsultationCreation` | idempotent draft creation |
| `IConsultationDeletion` | tombstone + cleanup + outbox, atomically |
| `IConsultationFileRegistration` | committing an uploaded file's blob reference |
| `IConsultationListing` | the read-only list/history queries |
| `IConsultationPatientAssignment` | attaching a patient to an unassigned draft |
| `IConsultationStructuredDataApproval` | marking Completed once structured data is approved |
| `IConsultationRetryStore` | requeuing a failed consultation |

All under [backend/src/MedicalAssistant.Application/Contracts/Persistence/](../../backend/src/MedicalAssistant.Application/Contracts/Persistence/), except `IConsultationRetryStore` at [Modules/CareWorkflow/Consultations/RetryConsultationProcessing/](../../backend/src/MedicalAssistant.Application/Modules/CareWorkflow/Consultations/RetryConsultationProcessing/IConsultationRetryStore.cs). DI registration: [ConsultationsPersistenceRegistration.cs](../../backend/src/MedicalAssistant.Persistence/Modules/CareWorkflow/Consultations/ConsultationsPersistenceRegistration.cs). Frontend: `frontend/src/modules/consultations/`.

**The legacy broad `IConsultationRepository` still exists** and is still the type two consumers depend on (`AssignConsultationPatientCommandHandler`... — no, those were already migrated; the remainder is `AskChatCommand.cs`'s unused injection and the two apparently-dead members `IdempotencyKeyExistsAsync`/`DeleteForDoctorAsync`). It's not removed as a side effect of any one slice — trimming it is a deliberate, separate decision.

## Invariants

- Every port is doctor-scoped: no method reads or writes a Consultation without a `doctorId` to authorize against (except the system-callback-triggered ports like `ITranscriptionCompletion`, which belong to Consultation Processing's seam, not this one).
- Deletion, file registration, and structured-data-approval each commit their state change and outbox event in one transaction — never partially visible.
- The strangler migration rule: a new port never removes a method from the broad interface as a side effect: [IConsultationDeletion.cs:5-11](../../backend/src/MedicalAssistant.Application/Contracts/Persistence/IConsultationDeletion.cs) states this explicitly.

## Dependencies

**Owns**: create/assign/upload/record/status/retry/delete, and cache policy on the frontend.
**Does not own**: patient identity (Patient Workspace owns Patients), transcription execution (Consultation Processing), or the transcript/structured-data content itself (Clinical Record) — only *approving* structured data to complete the consultation.

## Tests

- `backend/test/MedicalAssistant.Application.UnitTests` — handler-level unit tests.
- `backend/test/MedicalAssistant.Persistence.IntegrationTests` — real PostgreSQL via Testcontainers (Docker required).
- `backend/test/MedicalAssistant.Architecture.Tests` — enforces that `Application` never depends on a concrete adapter for these ports (R38).

## Runbook & known risks

Runbook: [docs/runbooks/consultation-lifecycle.md](../runbooks/consultation-lifecycle.md). Known risks tracked in [docs/known-issues/refactor-baseline.md](../known-issues/refactor-baseline.md):

| ID | Finding |
|---|---|
| K02 | Deletion can race Transcript Ready acceptance and reactivate a tombstoned Consultation. |
| K03 | A blob can be orphaned if the database/outbox transaction fails after upload. |
| K09 | Consultation polling excludes important uploaded states and defaults to a one-day interval. |
| K17 | Capture retries can create orphan Consultation drafts. |
| K18 | Capture and conversation selection contain asynchronous race candidates. |
| K19 | A Patient-history behavior option is omitted from its query-cache key. |
