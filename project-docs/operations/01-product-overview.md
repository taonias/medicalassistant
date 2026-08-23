# Product Overview and End-User Value

> Historical product/architecture snapshot. Use the [canonical system map](../../docs/architecture/system-map.md) for the current three-context runtime.

## What the product is

Medical Assistant is a doctor-facing workspace for capturing consultations and turning fragmented clinical material into an organized, searchable patient record.

The current application combines patient management, audio recording and PDF upload, transcription, consultation review, doctor notes, structured-data review, dashboard analytics, and chat-oriented interfaces. A newer AI subsystem adds a carefully designed clinical-document ingestion and evidence-grounded answer engine, although that subsystem is not yet connected to the main backend contract.

## The problem it addresses

Clinical information is spread across conversations, recordings, transcripts, notes, laboratory PDFs, imaging reports, and historical consultations. This creates recurring work for clinicians:

- Reconstructing what happened across previous encounters.
- Re-listening to recordings or reopening documents to find one detail.
- Manually converting free text into structured summaries.
- Remembering which files have finished processing and which have failed.
- Verifying whether an AI-generated answer really came from the patient's record.

The product's purpose is to reduce that retrieval and documentation burden while keeping the doctor in control of clinical interpretation and approval.

## Primary users

### Doctors

Doctors are the direct application users. They sign in, manage their own patients, record or upload consultations, review transcripts and structured data, write notes, and ask questions in general, patient, or consultation context.

### Patients

Patients do not directly operate the current system, but they are the subjects of its records. Their value is indirect: a clinician can spend less time reconstructing history, has clearer longitudinal context, and can correct or erase information through governed workflows.

### Clinic operators and compliance staff

Operators keep databases, storage, queues, AI providers, and processing workers healthy. Compliance staff care about patient isolation, provenance, auditability, EU-region processing, un-ingestion, and erasure.

## Value for doctors

- **Faster capture:** a doctor can record a consultation in the browser or upload existing audio/PDF material.
- **Less manual review:** audio is converted into a transcript and presented alongside the original recording.
- **One patient view:** consultations, notes, summary material, and history are grouped under the patient record.
- **Visible workflow state:** consultation status communicates whether material is uploaded, transcribing, transcribed, awaiting structured-data approval, complete, or failed.
- **Flexible working style:** an unassigned recording can be captured first and attached to a patient later.
- **Longitudinal recall:** the AI design indexes transcripts, notes, lab reports, and imaging findings as patient-scoped evidence.
- **Verifiability:** the grounded-answer design returns source identity, clinical date, source location, a bounded quote, and similarity score with each citation.
- **Human control:** structured data requires explicit approval; proposed chat actions require confirmation before execution.
- **Greek and English support:** Azure Speech is configurable by locale, while the AI retrieval design uses multilingual embeddings and answers in the question's language.

## Value for patients and clinics

- **Reduced omission risk:** relevant historical material can be surfaced without relying only on memory.
- **Traceable source use:** citations and source links let a clinician verify the underlying record.
- **Data correction:** a corrected AI document supersedes the earlier derived chunks instead of leaving contradictory versions searchable.
- **Privacy controls:** the AI service distinguishes ordinary un-ingestion from administrative GDPR erasure.
- **Operational transparency:** audit logs, processing states, quality reports, and telemetry make failures visible rather than silently losing work.
- **Practice insight:** the dashboard summarizes patient coverage, consultation volume, status distribution, failures, and unassigned recordings.

## Product promise versus current delivery

The complete product promise is: capture a consultation or clinical document, process it safely, make it available in a patient-scoped knowledge base, and answer questions only from verified patient evidence.

The repository currently delivers that promise in two partially disconnected halves:

1. The frontend, main backend, RabbitMQ, and transcriber form the connected doctor workflow for patient management, upload, audio transcription, and review.
2. The .NET 10 AI service implements document ingestion, pgvector retrieval, summaries, un-ingestion/erasure, and grounded answers behind a different API contract.

Connecting those halves is the highest-value integration step. See [Current state and gaps](10-current-state-and-gaps.md).

## Responsible positioning

The strongest value proposition is record organization and evidence-grounded assistance. The application should not claim to diagnose, prescribe, or guarantee improved clinical outcomes without separate validation, regulatory review, and product controls. An answer is an aid to a licensed clinician, and the original source remains authoritative.

