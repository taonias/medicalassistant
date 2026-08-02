---
status: accepted
date: 2026-08-02
---

# Separate audio Transcription from Document Processing

The Transcription Worker will consume only `consultation.audio-uploaded.v1`. Non-audio files publish `consultation.document-uploaded.v1` for a separate Document Processing capability. Until that capability is implemented, a document remains explicitly pending document processing and no Transcript is created.

This amends the generic `consultation.file-uploaded.v1` contract listed when ADR 0004 was first accepted. We rejected routing both file kinds through one generic event because direct-exchange subscribers would need to inspect payloads and the Transcription Worker would receive work it does not own. We rejected the current placeholder Transcript because it labels unprocessed content as completed clinical text.

## Consequences

- The initial upload publisher selects the routing key from the validated file kind and writes that exact event to the outbox.
- Audio and document events share envelope conventions but have separate payload contracts and consumers.
- The Transcription Worker contains no document branch and never invokes Azure Speech for documents.
- Product status distinguishes `AwaitingTranscription` from `AwaitingDocumentProcessing` (exact enum names may follow existing domain conventions).
- The future Document Processor may use Azure Document Intelligence or another extraction strategy without changing or redeploying the Transcription Worker.
- Migration tests verify that existing document uploads no longer produce placeholder Transcript records.
