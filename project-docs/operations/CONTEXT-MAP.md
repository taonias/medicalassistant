# Medical Assistant Context Map

The repository contains two related domain contexts with different models and data ownership.

## Contexts

- [Care Workflow](contexts/care-workflow/CONTEXT.md) — manages doctors, patients, consultations, recordings/documents, transcripts, notes, structured data, and clinician-facing actions.
- [Clinical Knowledge](contexts/clinical-knowledge/CONTEXT.md) — transforms clinical documents into searchable evidence and produces patient-scoped grounded answers.

## Relationships

- **Care Workflow → Clinical Knowledge:** Care Workflow is intended to submit explicit clinical Documents on behalf of an authenticated doctor. Clinical Knowledge trusts the backend-supplied doctor and patient identifiers.
- **Clinical Knowledge → Care Workflow:** Clinical Knowledge returns ingestion status, patient document/summary views, and grounded answers. It does not own the doctor-facing application or user authentication.
- **Shared concepts, separate identifiers:** Care Workflow uses integer patient and consultation IDs; Clinical Knowledge represents external doctor, patient, session, note, and report identifiers as strings in its boundary contract.
- **Conversation ownership:** Care Workflow is the intended owner of conversation state. Clinical Knowledge accepts bounded prior context but stores no chat messages.
- **Current integration state:** the intended relationship is documented and both sides have code, but their current HTTP/message contracts are incompatible. See [Current state and gaps](10-current-state-and-gaps.md).

