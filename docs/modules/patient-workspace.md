# Patient Workspace

## Purpose

Owns a Doctor's Patient directory: listing, creating, viewing a Patient's details and history filters, and the recent-patients convenience state — distinct from anything about a specific Consultation's own processing.

## Public interface / seam

- **Backend**: `IPatientRepository` at [Contracts/Persistence/IPatientRepository.cs](../../backend/src/MedicalAssistant.Application/Contracts/Persistence/IPatientRepository.cs), backed by `PatientRepository` and registered via [PatientsPersistenceRegistration.cs](../../backend/src/MedicalAssistant.Persistence/Modules/CareWorkflow/Patients/PatientsPersistenceRegistration.cs) (R38).
- **Frontend**: `frontend/src/features/patients/` — directory, create, and detail/history views; `useRecentPatients.ts` ([frontend/src/shared/hooks/](../../frontend/src/shared/hooks/useRecentPatients.ts)) is shared infrastructure this module depends on, not its own.

## Invariants

- Recent-patients is client-local state, not a backend feature: `frontend/README.md`'s own "Notes" section states it plainly — "Patient list uses recent patients stored locally because the backend does not expose a paginated patient search endpoint yet." A future paginated search endpoint is a real gap, not something this module should be assumed to already have.
- Patient history assembly spans Consultation Lifecycle's `IConsultationListing` (see [consultation-lifecycle.md](consultation-lifecycle.md)) — Patient Workspace itself owns only the Patient record; the consultations *in* that history belong to the other module.

## Dependencies

**Owns**: patient directory, details, history filters, recent-patient state, and the patient API.
**Does not own**: consultation processing internals — it reads consultation history through Consultation Lifecycle's own read ports, never a Patient-owned duplicate.

## Tests

`backend/test/MedicalAssistant.Application.UnitTests` (patient handler tests), `backend/test/MedicalAssistant.Persistence.IntegrationTests` (real PostgreSQL); frontend `useRecentPatients.contract.test.tsx`.

## Runbook & known risks

Runbook: [docs/runbooks/patient-workspace.md](../runbooks/patient-workspace.md). One known risk tracked in [docs/known-issues/refactor-baseline.md](../known-issues/refactor-baseline.md) (shared with [consultation-lifecycle.md](consultation-lifecycle.md) — it's a real cross-cutting finding, not duplicated by mistake):

| ID | Finding |
|---|---|
| K19 | A Patient-history behavior option is omitted from its query-cache key. |
