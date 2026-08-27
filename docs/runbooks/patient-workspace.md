# Patient Workspace Runbook

## Operating principle

There is no merge, delete, reassign, or admin-override capability for Patients anywhere in this application today. Diagnosis here is almost entirely about *reading* the right thing (which read path, which cache, which status code) — not about repairing state, because no state-repair tool exists. Where that's true below, it says so plainly rather than describing a procedure that doesn't exist.

## Suspected duplicate patient records

1. Two Patient rows with matching first name, last name, and date of birth are not prevented by the application. Creation only enforces per-doctor uniqueness on `ExternalPatientId`, and only when it's supplied and non-blank. Two visually identical patients existing is expected to be possible — it is not evidence of a bug by itself.
2. There is no merge operation. Do not attempt to consolidate two Patient rows by hand in the database: every table that references a Patient (Consultation, Conversation, DoctorNote) uses a restrictive foreign key, and nothing in the schema supports repointing those rows safely outside the application's own write paths.
3. If a doctor filed a consultation under the wrong (duplicate) patient, there is also no reassign/unassign operation — assigning a patient to a consultation explicitly refuses to touch a consultation that already has one. Recovering from this today is a support/engineering conversation, not an operator action.

## A doctor can't find a patient, or a patient "disappeared"

1. Patient reads return 404 for both "doesn't exist" and "exists but belongs to another doctor" — this is deliberate; the API never distinguishes the two with a 403. If a doctor reports a patient missing, this is expected behavior for a misrouted/mistyped ID as much as for anything else.
2. There is no admin or support role override on any patient endpoint — even an Administrator-flagged account is scoped to only their own patients when calling these endpoints. Investigating a cross-doctor patient question requires direct, authorized database access; there is no API path for it.

## Patient history looks wrong, stale, or is slow to load

1. If both a from-date and a to-date are supplied and the range exceeds one month, the request is rejected before any data is read. If only one of the two is supplied, no range-length check runs at all — an open-ended history request being slower is expected, not broken.
2. Structured-data summaries are fetched one query per consultation, and that lookup runs by default (`includeStructuredData` defaults to true). A patient with a long, unpaged consultation history can produce a genuinely slow load from this — it's a real, currently-unaddressed performance characteristic, not something tunable at runtime.
3. An unhandled database error during history assembly returns a generic, patient-agnostic error to the browser. Diagnose from backend logs/APM around the request timestamp, not from anything in the response body.
4. A consultation deleted while a doctor is viewing that patient's history does not disappear from the list. Deletion is a tombstone — the consultation's status flips to Deleted — and history assembly does not filter deleted consultations out. Seeing a "Deleted" row in patient history is expected, not evidence of a failed or reverted deletion.
5. If a doctor reports that the same-looking history view shows structured-data summaries inconsistently — present on one load, missing on the next, for what looks like an identical request — that matches a known, located defect: the frontend's patient-history cache key omits whether structured data was requested, so two requests differing only in that flag can be served each other's cached result. This is a real code defect, not confirmed to be firing in production today; treat it as an engineering escalation, not something to work around operator-side.

## Recent-patients list doesn't match a patient's current record

1. This is expected. The recent-patients list is a client-local snapshot stored in that browser's local storage, capped at ten entries, and is never refreshed from the server — it only updates when that patient is viewed, created, or updated again in that same browser.
2. An empty or unexpectedly reset recent-patients list is consistent with cleared or private-browsing storage; the underlying code degrades to an empty list on any read failure rather than erroring.
3. There is no server-side equivalent to fall back to — there is no paginated patient-search endpoint yet, so recent-patients is the only quick-access mechanism that exists today.

## Two doctors (or two tabs) edited the same patient at once

1. Patient updates carry no concurrency check. Two simultaneous edits resolve silently as last-write-wins — the later save simply becomes the current record, with no conflict ever surfaced to either editor.
2. There is nothing to fix after the fact; the later write is the current state by design (or, more precisely, by the absence of a concurrency design here). Recurring complaints about this are an engineering fix (a concurrency token), not an operator remediation.

## Known gaps — do not invent a fix for these

- No duplicate-patient detection, by name/date-of-birth or otherwise, at creation, update, or the database level.
- No merge, no delete, and no reassign-consultation-to-different-patient capability anywhere in the application.
- No admin/support role override on any patient read.
- No optimistic concurrency on patient updates.
- Patient-history query cache key omits whether structured data was requested, risking a stale/mismatched cache hit.

## Prohibited operator actions

- Editing `Patients` rows directly to merge duplicates — every referencing table uses a restrictive foreign key; a manual merge will either fail outright or silently orphan rows outside the application's own tracked invariants.
- Reassigning a consultation's patient by hand in the database to correct a mis-filed consultation — this bypasses the audit trail and outbox events the application relies on for that consultation.
- Deleting a Patient row directly — the application has no delete path for this by design; a manual delete will violate the same restrictive foreign keys unless every dependent Consultation/Conversation/DoctorNote is removed first, which is not a decision to make outside a reviewed data-repair process.

## See also

- [Patient Workspace module doc](../modules/patient-workspace.md) — ownership, seam, and architecture.
- [Consultation Lifecycle runbook](consultation-lifecycle.md) — a patient's consultation history spans both modules.
- [Known-issue ledger](../known-issues/refactor-baseline.md) — K19 (patient-history cache key).
