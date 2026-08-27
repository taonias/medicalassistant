# Consultation Lifecycle Runbook

## Operating principle

There is no enforced state machine behind a Consultation's status — every transition unconditionally overwrites the prior value, and nothing except the two upload-time guards checks what the current status was first. Diagnosis here means confirming what actually happened (status, timestamps, audit fields) before assuming a specific mechanism caused it; several of the scenarios below look identical from the outside but have different, verified causes.

## Retry a failed consultation

1. Retry only does something when the consultation is genuinely in one of two retryable shapes:
   - **Status is Failed and audio is available** → re-queues transcription. Status resets to `AudioUploaded`, the failure reason clears, and a fresh event re-drives the transcription worker. The already-uploaded audio blob is reused; retry never asks for a re-upload.
   - **Status is not Failed but a failure reason is still set, and a completed transcript exists** → re-queues Clinical Knowledge ingestion. Status does not change; only the failure reason clears.
   - **Anything else** → rejected with "This consultation has no failure to retry." This is the same message whether the consultation is already Completed or actively mid-processing — there is no separate "already in progress" response.
2. A rejection naming missing audio or a missing/incomplete transcript is telling you the precondition for that specific retry path is absent — check the audio blob reference or the transcript row before assuming the retry mechanism itself is broken.
3. If the underlying audio blob was itself lost (see "orphaned blob" below), retry cannot succeed, and it fails with the same missing-audio message as any other case — the two are not distinguishable from the retry response alone.

## A consultation is not progressing

1. There is no built-in "find consultations stuck for more than N minutes" tool — not an endpoint, not a script. The analytics endpoint reports a status breakdown with no time dimension; the drafts endpoints list Draft-status consultations with no time-in-status filter either. Confirming a consultation is genuinely stuck means checking its status and last-updated time directly, which today is a database-level investigation, not an application feature.
2. Before treating "the doctor's screen isn't updating" as proof of a stuck consultation, know the client's own polling gap: the frontend only polls while status is Transcribing, Transcribed, StructuredDataPending, or DocumentProcessingPending, and its default interval is 24 hours. `AudioUploaded` and `DocumentUploaded` are not polled at all — a consultation waiting for the worker to even pick it up will not visibly update in the UI, however fast it's actually processed on the backend. Check the real status server-side first.
3. Once a consultation is confirmed stuck in Failed, use the retry procedure above. For any other status, there is no supported recovery action today short of an engineering-reviewed data fix.

## Multiple Draft consultations from one recording attempt

1. Both capture flows (browser recording and file upload) create the Consultation row *before* uploading the file, using a new idempotency key on every attempt — including retries. A failed upload followed by the doctor retrying creates a second Consultation row instead of reusing the first; the first is left behind as an empty Draft.
2. This is a known, currently-unmitigated gap. No cleanup job removes these drafts automatically.
3. Unattached orphans (no patient assigned) and patient-attached Draft-status orphans are both listable, but neither list distinguishes "genuinely abandoned" from "in progress right now." Treat a listed draft as a starting point for a conversation with the doctor, not something to bulk-delete.

## A blob appears to be missing, or an upload silently failed to commit

1. File upload writes to blob storage before it writes to the database, with no rollback if the database write then fails. A blob can end up in storage with nothing — no Consultation row, no field — ever pointing back to it. This is a real, currently-unmitigated permanent-orphan risk, not a hypothetical.
2. There is no reconciliation job that finds these. The only blob-cleanup path that exists today runs off an already-persisted Consultation's own deletion event, so it can only ever clean up a blob a Consultation row already references.
3. If a doctor reports "I uploaded but nothing shows up," and there's no corresponding Consultation row or blob reference at all, that's consistent with this gap — there is nothing to restore, since the failed attempt was never a durable Consultation to begin with. Ask the doctor to re-upload.

## A deleted consultation reappeared with a different status

1. This matches a known race: deletion doesn't coordinate with an in-flight Clinical Knowledge acceptance for that same consultation's transcript. If a delete lands in the window between the transcript-ready acceptance being recorded and the ingestion call actually completing, the completion step can unconditionally flip a just-tombstoned consultation from Deleted back to a live processing status.
2. If a consultation the doctor explicitly deleted is now showing `StructuredDataPending` or later, this race is the most likely explanation — check before assuming the delete itself failed.
3. Re-delete it. Marking a consultation deleted has no precondition on its current status, so deleting an already-affected consultation a second time is safe and re-tombstones it correctly.
4. There is no automated detection for this — it only surfaces if someone notices a consultation the doctor deleted is still visible or active.

## What "deleted" actually means right now

1. The tombstone itself is synchronous and durable: the delete call only returns success after the status flip, deletion timestamp, and cleanup/outbox records are committed together.
2. Blob deletion and Clinical Knowledge un-ingestion happen afterward, asynchronously (the outbox relay polls on a short default interval), and neither is surfaced anywhere in the UI. Don't describe a deletion as "fully complete" the moment the delete call succeeds — the record is gone from the doctor's own view, but dependent cleanup is still converging.
3. Deleted consultations are not filtered out of any read path on the backend. A direct lookup, or a refresh of a patient's consultation list, still returns a deleted consultation with status "Deleted." The record vanishing from the doctor's own screen right after deletion is a client-side convenience (the frontend drops it from its local cache on success), not a server-side filter — seeing it reappear, still marked Deleted, on a fresh list load is expected.

## Status transitions — what to expect when investigating

1. There is no enforced state machine. Nearly every status-setting operation unconditionally overwrites the current status; only file upload checks the prior status first (and only to block uploading onto a Transcribing or Completed consultation). An unusual status sequence in the record is not, by itself, evidence of corruption — the application does not prevent unusual sequences.
2. The `Transcribing` status is defined but never actually set by any current code path — a consultation goes straight from `AudioUploaded` to `Transcribed` or `Failed`. Don't expect to observe a consultation sitting in `Transcribing`.

## Known gaps — do not invent a fix for these

- No stuck-consultation detection tool.
- No blob-orphan reconciliation.
- No protection against orphan Draft consultations from capture retries.
- A race between deletion and transcript-ready acceptance can reactivate a tombstoned consultation.
- No enforced consultation state machine.
- Client polling excludes `AudioUploaded`/`DocumentUploaded` and defaults to a 24-hour interval.

## Prohibited operator actions

- Manually setting a Consultation's status in the database to "unstick" it — nothing prevents this, which is exactly why it's dangerous: it bypasses the events that downstream processing (the transcription worker, Clinical Knowledge) relies on to learn anything changed.
- Deleting orphaned Draft rows found via the drafts endpoints without confirming with the doctor first — a draft that looks orphaned may be one the doctor is about to attach.
- Manually inserting or repairing a blob reference — this bypasses the event that would normally accompany a real upload commit, and leaves the record inconsistent with how the application believes it got there.

## See also

- [Consultation Lifecycle module doc](../modules/consultation-lifecycle.md) — ownership, seam, and the nine capability ports.
- [Patient Workspace runbook](patient-workspace.md) — a patient's consultation history spans both modules.
- [Messaging and Recovery runbook](messaging-and-recovery.md) — the outbox/DLQ mechanics a Consultation's own events feed into once they leave this module.
- [Known-issue ledger](../known-issues/refactor-baseline.md) — K02 (deletion race), K03 (orphaned blob), K09 (polling gaps), K17 (orphan drafts).
