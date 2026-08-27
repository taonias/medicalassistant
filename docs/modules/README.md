# Module READMEs

Per R37: a README for each of the 10 [Ownership Map](../../helping_documents_to_ignore/medical-assistant-architecture-refactor-plan.xlsx) areas — purpose, public interface, invariants, dependencies, tests, runbook, known risks. Several areas span multiple folders and deployables (e.g. Patient Workspace spans `frontend/src/features/patients/` *and* backend `CareWorkflow/Patients`), so these live centrally here rather than in any one folder — same reasoning as [docs/contexts/](../contexts/).

This is a different document from a deployable's own dev-onboarding README (like [clinical-knowledge/README.md](../../clinical-knowledge/README.md)) or an operational runbook (like [messaging-and-recovery.md](../runbooks/messaging-and-recovery.md)) — each module README below cross-references those rather than duplicating them.

## All 10 areas

| Area | README |
|---|---|
| Frontend Platform | [frontend-platform.md](frontend-platform.md) |
| Identity & Preferences | [identity-and-preferences.md](identity-and-preferences.md) |
| Patient Workspace | [patient-workspace.md](patient-workspace.md) |
| Consultation Lifecycle | [consultation-lifecycle.md](consultation-lifecycle.md) |
| Clinical Record | [clinical-record.md](clinical-record.md) |
| Consultation Processing | [consultation-processing.md](consultation-processing.md) |
| Durable Messaging | [durable-messaging.md](durable-messaging.md) |
| Clinical Knowledge | [clinical-knowledge.md](clinical-knowledge.md) |
| Runtime & Data | [runtime-and-data.md](runtime-and-data.md) |
| Cross-system Quality | [cross-system-quality.md](cross-system-quality.md) |
