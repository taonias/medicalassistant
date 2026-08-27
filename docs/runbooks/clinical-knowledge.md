# Clinical Knowledge Runbook

Operational scope only — container startup, migrations, and the destructive-reset warning live in [clinical-knowledge/database/README.md](../../clinical-knowledge/database/README.md); this runbook doesn't repeat that.

## Operating principle

Ingestion is atomic and rerun-from-scratch by design: there is no partial-completion state to reason about, and a rerun either fully replaces the previous version in one transaction or leaves the previous Completed version untouched. Most of what looks like a defect below is instead a known, currently-unfinished piece of the design — the sections after the working procedures name those plainly so they aren't mistaken for new incidents.

## Re-run a failed ingestion

1. Retrying a failed ingestion restarts it from its originally stored content — fully from scratch, no partial resume, and its attempt budget resets. Rejected if the ingestion isn't actually Failed, or if it's since been overtaken by a newer, already-completed submission for the same document identity.
2. Resubmitting the identical content to the same document identity does the same thing implicitly — there's no need to look up the ingestion ID first if the source system or doctor just resubmits.

## Correcting a document (new content, same identity)

1. Submitting different content under an existing document's identity creates a new ingestion; once it completes, it atomically supersedes the prior version's searchable chunks in the same transaction as the new ones landing. Retrieval is never briefly missing the document, and never sees both versions at once.
2. Resubmitting the *same* content under a different session or sequence number is treated as a no-op duplicate, not a correction — deduplication deliberately ignores where a document was filed, only what it contains (plus patient, doctor, document type, date, and language). Only genuinely different content for the same identity, or the same content for a different patient, produces new work.

## Remove one document, or erase all of a patient's data

1. Un-ingesting a document tombstones it (clears the stored raw content, records who/when) and removes its searchable chunks and any extracted analyte results. It's rejected if the document is still queued or actively processing — wait for it to reach a terminal state first.
2. Full-patient erasure is the separate GDPR path: it removes all of a patient's chunks, analyte results, and ingestion records, and writes its own audit entry. It requires a distinct admin credential from the service's everyday API key — a request using the ordinary key will be rejected.
3. **Important gap that applies to both of the above**: neither un-ingest nor erasure touches the patient's rolling Patient Summary. Only a *new successful ingestion* for that patient regenerates it. After removing the last document behind a summary line — or after a full erasure — the summary endpoint keeps returning the old text. For a full erasure specifically, this means the summary can go on describing clinical content the erasure was meant to remove. If a doctor or a compliance reviewer flags a summary that doesn't match current documents (especially after an erasure), treat it as this known gap and escalate it — it is not a caching delay to wait out.

## A worker instance crashed mid-ingestion

1. This self-heals without operator action. A recovery sweep runs on startup and on a short fixed interval: it finds every ingestion still marked queued or processing, checks which ones are genuinely still held by a live instance, and re-queues exactly the ones that are running unlocked because their owning process died.
2. If an ingestion still hasn't progressed after two sweep intervals, that's a sign of something deeper than a normal crash (the queue itself isn't draining, or every instance is unhealthy) — escalate rather than waiting longer.

## Ingestion status isn't updating live

1. The live status feed is explicitly best-effort: a failed push is logged and swallowed, never affects the ingestion's real outcome, and a dropped connection loses events with no server-side replay.
2. The supported recovery is client-side resync, not a server action — a reconnecting caller is expected to ask for current status directly, which always reflects the authoritative record. If status "seems frozen," confirm the caller is actually resyncing on reconnect before investigating anything server-side.
3. For session-transcript ingestions specifically, a Failed outcome also travels a separate, durable channel independent of the live feed, so the backend's own handling of a failed transcript ingestion never depends on that feed being connected.

## A grounded-chat answer looks ungrounded or oddly confident

1. Citation checking only verifies that every citation label the model actually wrote was one it was actually given — it does not require the model to cite anything at all. An answer with no citations can still come back as a normal, non-refused answer even when supporting evidence existed. If you see a confident-sounding answer with no citations, that's this known gap, not a retrieval failure to chase.
2. The retrieval confidence threshold defaults to 0.0 in this codebase, meaning by default it filters out only completely unrelated evidence, not merely weak evidence. If retrieved evidence looks marginal, check whether this environment has actually set a calibrated threshold; if not, that's the expected current default, not a regression.

## A provider (OpenAI/Azure) error surfaced somewhere unexpected

1. There is no sanitization anywhere in the ingestion failure path today. A raw provider exception message — which can include endpoint or deployment detail — is stored verbatim as the ingestion's error, pushed verbatim over the live status feed, and, for session transcripts, published verbatim in the durable event the backend consumes. Provider-detail text showing up in a failure reason or an error response is expected under the current implementation, not a new leak to chase down as its own incident — though it remains worth flagging if the specific content is sensitive enough to matter for a given deployment.
2. On the synchronous chat endpoint, only a citation-verification failure is handled explicitly; any other exception (including a raw provider exception) is not caught by this service's own code.

## A lab report shows no extracted analyte values

1. This can be entirely expected, not a failure: lab-report analyte extraction is deliberately all-or-nothing — if it can't be verified, it stores zero analyte rows and flags extraction as unsuccessful, but the ingestion itself still completes and the rendered report text is still stored and searchable.
2. Check the ingestion's own outcome: Completed with no analytes extracted is the conservative, working-as-designed path. Failed is the actual failure case worth investigating.

## Ingestions start failing right after an embedding-model or deployment change

1. The vector column has a fixed dimension. There's a startup guard for the Azure OpenAI embedding path — a mismatched configured dimension refuses to start the service — but no equivalent guard for the plain OpenAI embedding path, whose default model (if none is explicitly configured) isn't the same dimensionality as the schema expects.
2. If ingestions in an OpenAI-configured environment start failing at the embedding-write step after a model or config change, check the configured embeddings model and dimensions first — this is a currently-unguarded configuration risk, not necessarily a code regression.

## Known gaps — do not invent a fix for these

- Un-ingest and full patient erasure never regenerate or clear the Patient Summary; only a new successful ingestion does.
- Embedding-dimension mismatch is guarded only on the Azure OpenAI path, not the plain OpenAI path.
- A non-refused grounded-chat answer can carry zero citations.
- Retrieval's confidence threshold defaults to an uncalibrated 0.0.
- Raw provider exception text crosses into durable events, the live status feed, and HTTP responses unsanitized.
- No bulk-reprocess or admin endpoint beyond single-document retry and un-ingest.

## Prohibited operator actions

- Manually deleting or editing ingestion, chunk, or analyte rows directly in the database instead of using un-ingest or erasure — those endpoints are the only paths that keep the tombstone, the audit log, and the in-flight guard consistent with each other.
- Replaying a failed ingestion's stored content by hand outside the retry endpoint — retry already does this atomically and resets the attempt budget; a manual replay risks creating a second, untracked ingestion for the same identity.
- Treating a zero-citation grounded answer, or a stale Patient Summary after erasure, as data corruption requiring a database fix — both are known, tracked application gaps, not corruption.

## See also

- [Clinical Knowledge module doc](../modules/clinical-knowledge.md) — ownership, seam, and invariants.
- [clinical-knowledge/database/README.md](../../clinical-knowledge/database/README.md) — container, migrations, destructive-reset warning (narrower, database-only scope).
- [Messaging and Recovery runbook](messaging-and-recovery.md) — the Transcript Ready → Clinical Knowledge handoff and its failure modes are covered there, not repeated here.
- [Known-issue ledger](../known-issues/refactor-baseline.md) — K26, K27, K28, K29, K33.
