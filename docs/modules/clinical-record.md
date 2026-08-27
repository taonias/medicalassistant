# Clinical Record

## Purpose

Owns the clinical content produced from a Consultation: the editable Transcript, the doctor-reviewed Structured Medical Data, and free-form Doctor Notes. Where a doctor reads and corrects what came out of processing, distinct from Consultation Lifecycle (which owns the consultation's own state) and Clinical Knowledge (which owns retrieval over the ingested copy).

## Public interface / seam

Three repositories, each in its own module folder:

| Port | Module |
|---|---|
| `ITranscriptRepository` | [Contracts/Persistence/ITranscriptRepository.cs](../../backend/src/MedicalAssistant.Application/Contracts/Persistence/ITranscriptRepository.cs) |
| `IMedicalStructuredDataRepository` | [Modules/CareWorkflow/StructuredMedicalData/IMedicalStructuredDataRepository.cs](../../backend/src/MedicalAssistant.Application/Modules/CareWorkflow/StructuredMedicalData/IMedicalStructuredDataRepository.cs) |
| `IDoctorNoteRepository` | [Modules/CareWorkflow/DoctorNotes/IDoctorNoteRepository.cs](../../backend/src/MedicalAssistant.Application/Modules/CareWorkflow/DoctorNotes/IDoctorNoteRepository.cs) |

DI registration: `TranscriptsPersistenceRegistration.cs`, `StructuredMedicalDataPersistenceRegistration.cs`, `DoctorNotesPersistenceRegistration.cs`, each in its own [Modules/CareWorkflow/](../../backend/src/MedicalAssistant.Persistence/Modules/CareWorkflow/) subfolder. Frontend: `frontend/src/modules/clinical-record/`.

**Shared seam with Consultation Processing**: `ITranscriptionCompletion` and `IStructuredDataCompletion` (both on `ConsultationRepository`, not a Clinical Record type) are how the transcription and structured-data-extraction pipelines report a lifecycle transition back onto the Consultation — see [consultation-lifecycle.md](consultation-lifecycle.md).

## Invariants

- A Transcript can only be edited once it has been populated (`UpdateTranscriptCommandHandler` rejects an edit on an empty transcript).
- Structured Medical Data stays provisional (`Approved = false`) until a Doctor explicitly approves it — approval is what completes the Consultation ([IConsultationStructuredDataApproval](consultation-lifecycle.md)), not a StructuredMedicalData-side action.
- A Doctor Note may be patient-level (no Consultation) or consultation-level; an edited note re-POSTs the same id as a correction, matching the ingestion side's Correction concept.

## Dependencies

**Owns**: transcript review/editing, structured medical data, clinician notes.
**Does not own**: vector retrieval internals (Clinical Knowledge), the consultation's own lifecycle state (Consultation Lifecycle), or transcription execution (Consultation Processing) — this module only receives the *result*.

## Tests

- `backend/test/MedicalAssistant.Application.UnitTests`
- `backend/test/MedicalAssistant.Persistence.IntegrationTests` (real PostgreSQL)

## Runbook & known risks

No dedicated runbook yet. One relevant tracked risk in [docs/known-issues/refactor-baseline.md](../known-issues/refactor-baseline.md):

| ID | Finding |
|---|---|
| K06 | Legacy transcription callbacks lack the current event path's state gates and atomicity — affects how a Transcript's completion is recorded here. |
