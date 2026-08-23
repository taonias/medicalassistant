# Capabilities and User Journeys

> Historical capability snapshot. Use the [canonical system map](../../docs/architecture/system-map.md) and [observable interface baseline](../../docs/architecture/observable-interface-baseline.md) for current behavior.

## Capability map

| Capability | End-user surface | Current status |
| --- | --- | --- |
| Doctor login, profile, password | Frontend + main backend | Connected |
| Patient create, edit, search, history | Frontend + main backend | Connected |
| Dashboard analytics | Frontend + main backend | Connected |
| Browser audio recording | Frontend | Connected to upload workflow |
| Audio/PDF upload and download | Frontend + backend + Blob Storage | Connected |
| Asynchronous audio transcription | RabbitMQ + transcriber + Azure Speech | Connected |
| Transcript display and manual edit | Frontend + main backend | Connected |
| Doctor notes | Frontend + main backend | Connected; AI indexing call uses legacy contract |
| Structured-data display and approval | Frontend + main backend | UI/API connected; producer is not present in the active pipeline |
| General/patient/consultation chat UI | Frontend + main backend | Connected to legacy AI client; incompatible with current AI service |
| Action proposal, confirmation, status | Frontend + main backend | Workflow exists; external executor contract is not implemented here |
| Multi-document AI ingestion | .NET 10 AI service | Implemented but not connected to main backend |
| Patient-scoped vector retrieval | .NET 10 AI service | Implemented but not connected to frontend/backend |
| Grounded answer with citations/refusal | .NET 10 AI service | Implemented but not connected to frontend/backend |
| Document un-ingestion and patient erasure | .NET 10 AI service | Implemented API; no doctor-facing UI in this repository |

## Journey 1: Sign in and manage a patient

1. The doctor signs in with username and password.
2. The backend returns a JWT and user profile.
3. The browser persists the JWT and uses it for protected API requests.
4. The doctor opens the patient directory, searches existing patients, or creates a new record.
5. The patient view presents overview information, consultation history, patient-level notes, and links to record or chat.

Value: the doctor has a single working context for the patient's encounters rather than navigating disconnected files.

## Journey 2: Record now, attach later

1. The doctor opens **Record** and records audio in the browser.
2. A draft consultation may be created without a patient.
3. The audio is uploaded and stored in Blob Storage.
4. The dashboard lists it under **Unassigned recordings**.
5. The doctor can play the recording and attach it to one of their patients.

Value: capture is not blocked when the doctor wants to start immediately or patient selection is inconvenient.

Operational caveat: patient assignment updates the consultation but does not currently republish it for downstream AI ingestion. This matters if patient identity is needed after transcription.

## Journey 3: Record or upload a patient consultation

1. From a patient, the doctor starts a new consultation.
2. They choose audio capture/upload or PDF upload.
3. The backend validates file type and size, writes the file to Blob Storage, updates the consultation, and best-effort publishes `consultation.processing`.
4. The transcriber consumes the message, downloads the blob, audits each stage, and processes it.
5. For audio, Azure Speech produces transcript text. For a PDF, the transcriber currently creates placeholder transcript text rather than extracting the document.
6. The transcript and consultation status are written to the main database.
7. The frontend polls and displays the resulting status and transcript.

Value: the recording and its textual record remain attached to the encounter and can be reviewed without leaving the application.

## Journey 4: Review and correct a transcript

1. The doctor opens a consultation and reads or listens to the source.
2. They edit the populated transcript when needed.
3. The backend saves the corrected text and best-effort publishes `Transcript.Ready` to `consultation.transcript`.

Value: the clinician can correct speech-recognition errors before the transcript drives later assistance.

Gap: no active consumer of `consultation.transcript` exists in this repository, so the correction does not currently reach the new AI ingestion store.

## Journey 5: Review and approve structured data

1. The frontend requests the latest structured-data record for a consultation.
2. It renders the summary and typed sections from the stored JSON payload.
3. The doctor approves the result.
4. Approval marks the structured record approved and the consultation complete.

Value: machine-produced structure does not become final solely because a model returned it.

Gap: the active transcriber does not produce structured data, and the legacy callback producer is absent. This journey has a working review surface but no connected generator.

## Journey 6: Ask about a patient

### Current main-application path

The frontend sends a chat request to the main backend. The backend assembles a limited context object and calls the legacy AI endpoints (`/v1/chat`). Patient context currently contains patient identity and consultation dates/statuses; consultation context also contains transcript, structured data, and notes.

### Intended grounded path

The newer AI service accepts a patient ID and question, retrieves only that patient's document chunks from pgvector, rejects evidence below a threshold, and either:

- Generates a cited answer and validates that cited evidence labels were supplied; or
- Returns a deterministic insufficient-evidence response without calling the answer model.

Value: the doctor can ask instead of manually rereading, while citations preserve a route back to the source.

Gap: the main backend does not call the newer endpoint or transform its citation response.

## Journey 7: Correct or remove AI knowledge

The AI service distinguishes:

- **Correction:** resubmitting a document identity with different content atomically supersedes old chunks.
- **Continuation:** a new transcript sequence for the same session coexists as additional content.
- **Un-ingest:** removes one document and its derived data but leaves an audit tombstone.
- **Erasure:** an admin-authorized operation removes all data for a patient, including tombstones, and records the erasure act.

Value: wrong-patient uploads, edits, duplicates, and privacy requests have explicit semantics rather than ad hoc deletion.

