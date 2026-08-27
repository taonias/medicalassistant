# Consultation Processing

## Purpose

Owns turning a stored Consultation Recording into a Transcript: the audio-to-transcript workflow, speech-provider adapters, and the transaction outcomes/stale-work gates that make that processing durable and safe to redeliver.

## Public interface / seam

A standalone .NET Generic Host process (ADR-0001/0009), not a library another deployable references — [backend/src/MedicalAssistant.Transcription.Worker/](../../backend/src/MedicalAssistant.Transcription.Worker/):

- **Input**: `Host/ConsultationAudioUploadedIntegrationEventHandler.cs` — consumes `consultation.audio-uploaded.v1` (Durable Messaging owns the transport; see [durable-messaging.md](durable-messaging.md)).
- **Work**: `Application/TranscribeAudio/` — the R24 Transcribe Audio workflow.
- **Adapters**: `Infrastructure/Speech/` (`AzureSpeechTranscriptionService`, `ISpeechTranscriptionService`), `Infrastructure/Blob/` (`ConsultationAudioBlobRetriever`, `AzurePrivateBlobObjectClient`) — the exact set [LegacyRemovalCompletionTests.cs](../../backend/test/MedicalAssistant.EventBusRabbitMQ.UnitTests/LegacyRemovalCompletionTests.cs) (found during R39) asserts are present, as proof the legacy Azure Function's logic was ported rather than dropped.
- **Output**: `Transcript Ready` / `Transcription Failed` outbox events — the seam back to Clinical Record (see [clinical-record.md](clinical-record.md)) via `ITranscriptionCompletion`.

## Invariants

- Redelivery completes work exactly once: an inbox record keyed by integration-event ID makes a redelivered `consultation.audio-uploaded.v1` a safe no-op (ADR-0003).
- The worker checks Consultation deletion/revision state before committing a Transcript — a stale or superseded upload never resurrects deleted content.
- No stage checkpoints: a failure means a fresh rerun from the stored payload, never a resume (ADR-0003, referenced directly in `IngestionTransactionRunner`'s own doc comment on the Clinical Knowledge side of an analogous decision).

## Dependencies

**Owns**: the audio-to-transcript workflow, provider ports, transaction outcomes, stale-work gates.
**Does not own**: Patient UI, or generic RabbitMQ mechanics (Durable Messaging owns the wire, not this module).

## Tests

`backend/test/MedicalAssistant.Transcription.Worker.Tests` — 6 test files; `backend/test/MedicalAssistant.Persistence.IntegrationTests` for the shared durable-messaging/completion paths (real PostgreSQL); full-system Compose gate described in [messaging-and-recovery.md](../runbooks/messaging-and-recovery.md)'s "See also" end-to-end failure matrix.

## Runbook & known risks

Runbook: [docs/runbooks/messaging-and-recovery.md](../runbooks/messaging-and-recovery.md) covers this worker's own crash-loop and backlog scenarios directly. Known risks tracked in [docs/known-issues/refactor-baseline.md](../known-issues/refactor-baseline.md):

| ID | Finding |
|---|---|
| K06 | Legacy transcription callbacks lack the current event path's state gates and atomicity. |
| K32 | Backend success-state convergence after Clinical Knowledge ingestion is unclear. |
