# Refactor Risk and Known-Issue Baseline

Status: triage input for behavior-preserving refactoring

Verified against source and local checks at commit: ed2e38e

Baseline date: 23 August 2026

This is not a promise that every item is a confirmed defect. It separates known failures, security/deployment risks, and behavior requiring characterization from the structural refactor. Each fix should receive its own ticket, acceptance criteria, and tests.

## Severity definitions

| Severity | Meaning |
|---|---|
| Critical | Credible production security, privacy, or unrecoverable-data risk |
| High | Can break a core workflow, lose consistency, or prevent a safe release |
| Medium | Reliability, maintainability, or user-experience risk with a workaround |
| Low | Hygiene or clarity issue unlikely to break a core workflow alone |

## Defect and risk ledger

Every item has a stable ID and a related refactor row. “Related” means the refactor needs a characterization or ownership boundary around the risk; it does not authorize fixing the behavior in that refactor commit.

| ID | Severity | Finding | Separate action | Related refactor |
|---|---|---|---|---|
| K01 | Critical | Production database initialization mounts a local-only script that creates/changes a known-password PostgreSQL superuser. | Split local/common initialization and rotate deployed credentials before production use. | R04, R34 |
| K02 | High | Deletion can race Transcript Ready acceptance and reactivate a tombstoned Consultation. | Add a real PostgreSQL race test and state/revision gate in a separate fix. | R04, R05 |
| K03 | High | A blob can be orphaned if the database/outbox transaction fails after upload. | Decide compensation or reconciliation and test partial failures. | R04, R05 |
| K04 | High | Outbox leasing is PostgreSQL-specific while SQL Server remains advertised. | Decide supported providers or add a provider-specific adapter. | R04, R05 |
| K05 | High | RabbitMQ consumer handling drops cancellation in parts of the delivery path. | Add shutdown/redelivery tests, then propagate cancellation separately. | R04, R06 |
| K06 | High | Legacy transcription callbacks lack the current event path's state gates and atomicity. | Characterize usage, then fix or deprecate through a separate decision. | R04, R07 |
| K07 | High | ADR retry policy says five delayed retries while runtime RetryDelays is empty. | Make a product/architecture decision and update code/config/ADR together. | R04, R06 |
| K08 | High | Fresh frontend configuration targets HTTPS while root Compose exposes HTTP. | Establish one canonical local URL with a configuration test. | R04, R08 |
| K09 | High | Consultation polling excludes important uploaded states and defaults to a one-day interval. | Agree polling semantics and add browser tests before changing them. | R04, R09 |
| K10 | High | Release packaging is non-deterministic and can include stale files or secrets. | Rebuild from an empty staging directory and validate the manifest. | R04, R35 |
| K11 | High | release.info is not consumed by deployment, so packaged and deployed image tags can diverge. | Make deployment validate/consume release metadata. | R04, R35 |
| K12 | High | No automated test protects the complete product path through RabbitMQ, Worker, Clinical Knowledge, and browser. | Complete characterization Waves R05–R10 before broad moves. | R05–R10 |
| K13 | High | AutoMapper and SSH.NET restore with high-severity NU1903 advisories. | Triage reachability and upgrade with compatibility tests. | R04 |
| K14 | Medium | Identity password policy permits very short/simple passwords. | Make a security/product decision with migration and communication planning. | R04 |
| K15 | Medium | JWT material is persisted in browser localStorage. | Threat-model the frontend session and choose a deliberate storage strategy. | R04, R08 |
| K16 | Medium | Frontend session refresh can erase role information. | Reproduce and fix with auth-store tests. | R04, R08 |
| K17 | Medium | Capture retries can create orphan Consultation drafts. | Reproduce under controlled upload failures before capture consolidation. | R04, R09 |
| K18 | Medium | Capture and conversation selection contain asynchronous race candidates. | Add deterministic cancellation/ordering tests before fixes. | R04, R08, R09 |
| K19 | Medium | A Patient-history behavior option is omitted from its query-cache key. | Add a query-key characterization test, then correct separately. | R04, R08 |
| K20 | Medium | Standalone and root RabbitMQ Compose paths can conflict on container name/ports. | Declare the canonical path and guard the optional standalone path. | R04, R34 |
| K21 | Medium | The local start script prints database credentials. | Remove secret output and define safe diagnostic logging separately. | R04, R34 |
| K22 | Medium | The vector database UI image is mutable/unpinned. | Decide ownership/need and pin a reproducible image. | R04, R34 |
| K23 | Medium | Runbooks and generated graph output describe removed architecture. | Keep canonical docs current and regenerate/archive derived material. | R01, R36, R39 |
| K24 | Medium | Swagger appears enabled without a production environment guard. | Confirm intent and secure/disable it in a separate deployment change. | R04, R07 |
| K25 | Medium | Frontend lint has 9 errors and the production bundle is approximately 789 KB. | Create explicit lint-baseline and performance work. | R04, R08, R09 |
| K26 | High | Erasure and un-ingest can leave stale PatientSummary persistence records (code term; glossary term is now **Patient Summary**, see [docs/contexts/clinical-knowledge/CONTEXT.md](../contexts/clinical-knowledge/CONTEXT.md) — R36). | Add lifecycle regression tests and correct regeneration/removal separately. | R04, R10 |
| K27 | High | Default embedding model dimensions may not match the fixed vector schema. | Validate every provider/model dimension at startup. | R04, R10 |
| K28 | High | Citation verification permits a generated answer with no citations. | Make a Clinical Safety/product decision and change with focused tests. | R04, R10 |
| K29 | High | Production retrieval confidence threshold defaults to 0.0 and appears uncalibrated. | Calibrate and govern using evaluation data. | R04, R10 |
| K30 | High | Clinical Knowledge can mark an unroutable RabbitMQ event published. | Require routing/return confirmation with a real-broker test. | R04, R06 |
| K31 | High | A crash after Clinical Knowledge returns 202 can redeliver into a legitimate 409 without reconciliation. | Design an idempotent status-reconciliation flow and crash-window test. | R04, R06 |
| K32 | Medium | Backend success-state convergence after Clinical Knowledge ingestion is unclear. | Clarify the state model/completion contract before implementation. | R04, R06 |
| K33 | High | Raw provider exception messages can cross a durable service boundary. | Classify and sanitize failures with PHI/security tests. | R04, R10 |
| K34 | Medium | Chat context is not bounded at the HTTP boundary. | Define cost/model limits and test them separately. | R04, R10 |
| Q01 | Low | Repository-root helpers treat only a .git directory as valid and fail in Git worktrees. | Make test root discovery accept a .git file in a test-infrastructure change. | R04 |
| Q02 | Low | A Clinical Knowledge guardrail test compares platform line endings exactly. | Normalize line endings in the test without changing runtime chunking. | R04 |
| Q03 | Low | npm audit reports 1 moderate and 4 high frontend dependency advisories. | Triage and upgrade dependencies with frontend contract tests. | R04, R08 |

## Verified local baseline

These results describe the pre-refactor source at ed2e38e and are the comparison point, not an acceptance waiver:

| Check | Baseline result |
|---|---|
| Backend tests | 138 passed, 1 failed. The failing release-gate assertion expects service postgres-app while current Compose uses postgres. |
| Clinical Knowledge tests | 182 passed. |
| Acceptance tests | 6 passed, but the fixture disables RabbitMQ, runs no Clinical Knowledge workers, and does not exercise the Transcription Worker or browser. |
| Frontend build | Passed with an approximately 789 KB bundle warning. |
| Frontend lint | Failed with 9 reported issues. |
| Dependency restore | NU1903 advisory output observed for AutoMapper and SSH.NET. |
| Frontend dependency audit | npm reports 5 advisories: 1 moderate and 4 high. |

### Wave 0 isolated-worktree verification

- The root solution builds successfully with the Transcription Worker included (2 pre-existing NU1903 warnings, 0 errors).
- Application unit tests pass: 49/49.
- Acceptance tests pass: 6/6.
- Clinical Knowledge reports 181/182; the only failure is the line-ending-sensitive chunk guardrail listed above.
- Event contract and migration-ownership tests that inspect source paths fail because their root finder does not support a worktree .git file; the underlying projects compile.
- The release-gate suite retains its one known postgres-app versus postgres assertion failure.
- Frontend production build passes at 789.22 KB; lint retains 9 existing errors.
- Root Compose resolves all 11 expected service names.

## Refactor handling rules

- Preserve the baseline unless a separate approved bug-fix ticket says otherwise.
- Add characterization tests before moving a risky workflow.
- If behavior changes unexpectedly, revert or add a compatibility adapter; do not rewrite the baseline to make the refactor pass.
- Link each confirmed defect to a single owning bounded context and developer/team.
- Prioritize Critical and High items before broad folder moves in the same capability.
- Re-run the full verification matrix at the end of every wave and update this document only with evidence.
