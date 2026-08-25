---
status: accepted
date: 2026-08-02
---

# Let the main backend consume Transcript Ready and call Clinical Knowledge

The main backend will own a durable subscriber queue bound to `consultation.transcript-ready.v1`. Its idempotent handler loads the current Transcript plus the authorized doctor/patient context and submits a `SessionTranscript` to the Clinical Knowledge ingestion API. The event carries identifiers and revision metadata, not transcript text.

We rejected making the Transcription Worker call Clinical Knowledge because that would join speech recognition and knowledge ingestion into one failure and deployment boundary. We rejected putting transcript text on RabbitMQ because broker payloads, retries, dead letters, and backups would duplicate clinical content. We also rejected direct Clinical Knowledge consumption for now because its documented trust contract makes the backend its sole authenticated caller and the backend owns user authorization context.

## Consequences

- The Transcription Worker ends after it stores the Transcript and outboxes `consultation.transcript-ready.v1`.
- The main backend runs the eShop-style RabbitMQ consumer as a hosted service, but clinical-knowledge processing itself remains in the existing ingestion service.
- The handler uses its inbox and durable ingestion identity to tolerate event redelivery without creating duplicate knowledge documents.
- Initial transcription and subsequent clinician corrections publish distinct event IDs with increasing transcript revisions. The ingestion API treats a changed revision as a correction of the same document identity.
- If the backend or Clinical Knowledge API is unavailable, RabbitMQ retry/dead-letter policy protects the integration work; this does not roll back the completed Transcript.
- The backend checks that the Consultation and Transcript are still available before submitting, preventing stale ready events from restoring deleted data.
