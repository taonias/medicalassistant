# Security, Privacy, and Clinical Safety

This document describes mechanisms present in code and risks requiring remediation. It is not a compliance certification or clinical-safety approval.

## Trust boundaries

### Doctor to main backend

The main backend authenticates doctors with ASP.NET Core Identity and bearer JWTs. Patient, consultation, transcript, note, chat, and action handlers generally retrieve records through doctor-scoped repository methods.

### Main backend callbacks

Legacy AI callbacks bypass user JWT and require `X-Api-Key`. The callback controller compares the supplied key with one configured value.

### Main backend to new AI service

The AI service assumes one trusted internal caller and authenticates with shared API secrets. It does not validate per-user JWTs. Doctor/patient identifiers in payloads are trusted, while patient scope is enforced in AI retrieval SQL. Patient erasure requires a separate admin claim derived from the admin key.

This design is acceptable only if the AI service is network-restricted to trusted backend callers. Public exposure would invalidate the assumption.

## Patient isolation

- Main backend commands/queries normally fetch patients and consultations for the current doctor.
- AI retrieval requires patient ID and applies it in the same SQL query as vector similarity search.
- Optional AI doctor filtering narrows the corpus but is not a permission check.
- The main backend remains responsible for deciding whether the doctor may operate on the patient before calling AI.

Contract integration must preserve that authorization order. Passing an arbitrary frontend patient ID directly to the AI service would be unsafe.

## PHI locations

Potential protected health information exists in:

- Main relational database: demographics, consultation metadata, transcript text, notes, structured JSON, logs.
- Blob Storage: audio and PDF source material.
- RabbitMQ messages: identifiers, blob URI, filenames, and workflow metadata.
- AI PostgreSQL: raw ingestion payloads, chunks, summaries, analytes, provenance.
- Provider requests: speech audio, document PDFs, chunking/refinement/answer prompts, embedding text.
- Logs/audits: some transcriber audits currently include transcript previews up to 200 characters.
- Browser: JWT, patient data rendered in memory, and object URLs for downloaded media.

Telemetry policy in the AI service avoids question/answer/source text and carries IDs/counts only. That policy is stronger than the transcriber's audit behavior and should be made consistent across the estate.

## Clinical-safety mechanisms

### Source preservation during ingestion

For prose, the model proposes line boundaries and code reconstructs the chunk from original lines. For lab values, the model maps cells and code copies/verifies printed values. These designs reduce the risk of silently generated substitutions in stored source data.

Model-written context blurbs, document summaries, and patient summaries are still generated text and must remain labelled as derived content.

### Atomic visibility

The AI service commits superseded deletion, new chunks/derived rows, quality report, and completed status in one transaction. Retrieval should therefore not see a half-corrected document.

### Evidence threshold and refusal

When no retrieved chunk clears the configured threshold, the AI chat path returns a fixed Greek or English insufficient-evidence message without calling the answer model.

The default threshold in code is currently `0.0`, explicitly awaiting calibration. This is permissive and should not be treated as a clinically validated cutoff.

### Citation verification

The current verifier ensures that every `[E#]` label appearing in the answer corresponds to evidence supplied on that turn. It then returns only cited items.

It does **not** currently prove that:

- Every medical claim has a citation.
- The cited quote semantically entails the claim.
- The answer contains at least one citation.
- The model did not combine evidence incorrectly.

The design documents' stronger statement that every factual claim is proven against evidence is an aspiration beyond the implemented label-integrity check.

### Human approval and action confirmation

The doctor must approve structured medical data before the consultation becomes complete. Chat-suggested actions require an explicit confirmation modal before the backend submits an action request.

## Deletion semantics

- Deleting a main consultation is a care-workflow operation and is separate from AI un-ingestion.
- AI **Un-ingest** removes one document's clinical content and derived rows but leaves a deleted tombstone.
- AI **Erasure** removes all patient AI data, including tombstones, and records the erasure act.

The main database and Blob Storage do not currently call those AI lifecycle endpoints. A complete privacy workflow must coordinate deletion across every store and define what the retained erasure log may contain.

## Critical security findings

### Secrets in tracked configuration

The main backend configuration contains a live-looking Azure Storage account key, JWT signing material, RabbitMQ credentials, and development AI keys. The storage credential should be treated as compromised and rotated. Secrets should move to a managed secret store or environment-specific protected configuration.

### Weak password policy

Identity currently requires only three characters and no digit, case, or non-alphanumeric complexity. This is not suitable for a clinical production system. Registration is also anonymous and no email-confirmation gate is visible.

### Browser token storage

The JWT is persisted in local storage, which exposes it to successful cross-site scripting. A production threat model should decide between hardened browser-token controls and secure HttpOnly cookie-based sessions, alongside CSP and dependency hygiene.

### Callback/API-key hardening

The legacy callback controller has one static key and a simple ordinal comparison. The AI service has the more complete dual-key rotation/admin-key model. Standardize rotation, secret lifetime, constant-time comparison where practical, network restrictions, rate limiting, and audit of authentication failures.

### Swagger and service exposure

Swagger is enabled unconditionally in both APIs. AI Swagger is contract-only but still reveals endpoints. Production exposure should be deliberate and network/auth controls should be documented.

## Required governance decisions

- Approved EU regions and DPAs for Speech, OpenAI/Azure OpenAI, and Document Intelligence.
- Encryption, backup, restore, and key-management policy for both databases and Blob Storage.
- Retention periods for source media, transcripts, raw AI payloads, audit logs, tombstones, and erasure logs.
- Incident response for cross-patient retrieval or unintended provider disclosure.
- Clinical evaluation of transcription accuracy, chunking/retrieval quality, refusal behavior, citation quality, and bilingual performance.
- A visible product disclaimer and clinician verification workflow for generated content.
- Regulatory classification and change-control requirements before clinical deployment.

