# Runbooks

The [Ownership Map](../../helping_documents_to_ignore/medical-assistant-architecture-refactor-plan.xlsx) (sheet "Ownership Map") names roughly seven per-area runbooks (frontend platform, patient module, consultation lifecycle, worker/retry, messaging/DLQ, Clinical Knowledge, operations/recovery). This folder is not yet the complete consolidated set — it grows slice by slice.

## Consolidated here

| Area | Runbook |
|---|---|
| Messaging/DLQ, worker/retry, operations and recovery | [messaging-and-recovery.md](messaging-and-recovery.md) — RabbitMQ outages, outbox/DLQ replay, worker crash loops, disaster recovery, prohibited operator actions. See also its own "See also" link to the [end-to-end failure matrix](../../project-docs/event%20bus%20implementations/12-end-to-end-failure-matrix.md) for the test evidence behind each scenario. |
| Patient module | [patient-workspace.md](patient-workspace.md) — duplicate records, cross-doctor 404 semantics, history staleness/performance, concurrent edits. Also documents that no merge/delete/admin-override capability exists today. |
| Consultation lifecycle | [consultation-lifecycle.md](consultation-lifecycle.md) — retry eligibility, stuck consultations, orphaned drafts and blobs, the deletion/transcript-ready race, what "deleted" actually means right now. |
| Clinical Knowledge (operational) | [clinical-knowledge.md](clinical-knowledge.md) — retry/correction/un-ingest/erasure, worker crash recovery, ungrounded answers, provider-error leakage, lab-report Tier 2 behavior. Distinct from [clinical-knowledge/database/README.md](../../clinical-knowledge/database/README.md)'s narrower database-only scope. |

## Still scattered — not yet consolidated

| Need | Current doc |
|---|---|
| Run the whole stack locally | [HOW-TO-RUN.md](../../HOW-TO-RUN.md) |
| Full local end-to-end runbook | [project-docs/e2e-alignment/02-local-e2e-runbook.md](../../project-docs/e2e-alignment/02-local-e2e-runbook.md) |
| Clinical Knowledge database (pgvector container, migrations, destructive-reset warning) | [clinical-knowledge/database/README.md](../../clinical-knowledge/database/README.md) |
| Production deployment (Compose, nginx, Let's Encrypt) | [ops/deploy/DEPLOYMENT.md](../../ops/deploy/DEPLOYMENT.md) |
| RabbitMQ local provisioning | [ops/rabbitmq/](../../ops/rabbitmq/) (`provision.sh`, `docker-compose.yml`) |
| Frontend platform | [frontend/README.md](../../frontend/README.md) — dev-onboarding README, not an operational runbook; adequate as-is for now |

None of the consolidated or scattered docs above has been re-verified against the current post-refactor code paths as part of this slice — see [docs/known-issues/refactor-baseline.md](../known-issues/refactor-baseline.md) item K23 for the tracked gap.
