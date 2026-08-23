# Medical Assistant Context Map

For the canonical, source-verified runtime topology, deployables, data ownership, and developer ownership seams, see [System Map](docs/architecture/system-map.md).

Medical Assistant contains three related domain contexts. Each owns different language and responsibilities; integrations use explicit contracts rather than merging their models.

## Contexts

- [Care Workflow](project-docs/operations/contexts/care-workflow/CONTEXT.md) — supports Doctors in managing Patients, Consultations, Recordings, Consultation Documents, Transcripts, notes, structured data, and clinician-facing actions.
- [Consultation Processing](project-docs/event%20bus%20implementations/CONTEXT.md) — turns stored Consultation audio into a Transcript and announces processing outcomes through durable integration events.
- [Clinical Knowledge](AI/CONTEXT.md) — ingests declared clinical Documents into searchable evidence and produces patient-scoped Grounded Answers.

## Relationships

- **Care Workflow → Consultation Processing:** Care Workflow stores an authorized Consultation File and records either Consultation Audio Uploaded or Consultation Document Uploaded. Consultation Processing handles the asynchronous work appropriate to that fact.
- **Consultation Processing → Care Workflow:** Transcription shares the Consultation Processing data boundary, stores the Transcript/status/audit outcome, and records Transcript Ready or Transcription Failed.
- **Care Workflow → Clinical Knowledge:** The backend consumes Transcript Ready, loads the current authorized Transcript plus Doctor/Patient context, and submits a Session Transcript through the authenticated Clinical Knowledge HTTP boundary. Transcript text never travels on RabbitMQ.
- **Clinical Knowledge → Care Workflow:** Clinical Knowledge exposes durable Ingestion state, searchable documents, summaries, and Grounded Answers. It does not own Doctor authentication or the clinician-facing workflow.
- **Deletion across contexts:** Care Workflow records the authoritative deleted state and Consultation Deleted fact. Consultation Processing treats stale work as a no-op; Clinical Knowledge performs idempotent Un-ingest/Erasure behavior according to the approved policy.

## Language boundaries

- A Care Workflow **Recording** becomes a Consultation Processing **Transcript** through **Transcription**.
- A **Transcript** becomes a Clinical Knowledge **Document** only when the backend submits it for **Ingestion**.
- **Document Processing** extracts non-audio Consultation Documents and is separate from audio Transcription.
- RabbitMQ queues, retries, and dead letters are implementation/operational concepts, not domain records or business outcomes.

## ADR locations

- [Consultation Processing and event-bus ADRs](project-docs/event%20bus%20implementations/adr/)
- [Clinical Knowledge ADRs](AI/docs/adr/)
