# Care Workflow

Care Workflow supports a doctor in recording, organizing, reviewing, and acting on a patient's clinical encounters.

## People and records

**Doctor**:
The authenticated clinician who owns the patients and consultations visible through the application.
_Avoid_: User, operator, account

**Patient**:
A person whose clinical encounters and supporting material are managed by a Doctor.
_Avoid_: Client, subject, case

**Patient Record**:
The collected consultations, notes, summaries, and approved structured material associated with one Patient.
_Avoid_: Chart, profile

## Clinical workflow

**Consultation**:
A dated clinical encounter owned by a Doctor and optionally attached to a Patient while it is still a draft.
_Avoid_: Session, appointment, recording

**Unassigned Consultation**:
A draft Consultation captured before a Patient has been attached.
_Avoid_: Orphan recording, unattached patient

**Recording**:
The audio artifact captured or uploaded for a Consultation.
_Avoid_: Transcript, consultation

**Consultation Document**:
A PDF artifact uploaded for a Consultation.
_Avoid_: Recording, transcript, Clinical Knowledge Document

**Transcript**:
The editable textual record produced from a Consultation's Recording.
_Avoid_: Recording, note

**Doctor Note**:
Free-form clinician-authored text about a Patient, optionally associated with one Consultation.
_Avoid_: Transcript, annotation

**Structured Medical Data**:
Machine-produced structured content for a Consultation that remains provisional until a Doctor approves it.
_Avoid_: Summary, diagnosis, patient record

**Approval**:
The Doctor's explicit acceptance of Structured Medical Data, completing that Consultation's processing workflow.
_Avoid_: Save, publish

## Assistance

**Chat Query**:
A Doctor's question in general, Patient, or Consultation context.
_Avoid_: Grounded Answer, search

**Action Request**:
A Doctor-confirmed request for an external AI-assisted workflow, tracked by correlation identifier and status.
_Avoid_: Command, suggestion

**Suggested Action**:
An operation proposed in a chat response that requires explicit Doctor confirmation before becoming an Action Request.
_Avoid_: Automatic action, recommendation

