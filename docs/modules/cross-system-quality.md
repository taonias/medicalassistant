# Cross-system Quality

## Purpose

Owns full-system journeys, test harnesses, baseline snapshots, and dependency enforcement — proving the whole product works together, not any one module's own unit behavior (every other module owns that itself).

## Public interface / seam

- **Backend acceptance**: [tests/MedicalAssistant.AcceptanceTests/](../../tests/MedicalAssistant.AcceptanceTests/) — full-stack HTTP scenarios, `Contracts/` subfolder for the shared contract fixtures.
- **Browser end-to-end**: [frontend/e2e/](../../frontend/e2e/) — Playwright, organized by feature (`capture/`, `chat/`, `consultations/`, `routes/`), with its own `__screenshots__/` baseline snapshots.
- **Architecture enforcement** (R38, this session): [backend/test/MedicalAssistant.Architecture.Tests/](../../backend/test/MedicalAssistant.Architecture.Tests/) (backend layering, `NetArchTest.Rules`), `clinical-knowledge/tests/MedicalAssistance.Ingestion.Api.Tests/ModuleBoundaryTests.cs` (AI service module boundaries), [frontend/eslint.architecture.config.js](../../frontend/eslint.architecture.config.js) (frontend import boundaries).
- **Observable-interface snapshot**: [docs/architecture/contracts/observable-interface.snapshot.json](../architecture/contracts/observable-interface.snapshot.json), verified by [scripts/dev/verify-observable-interface.ps1](../../scripts/dev/verify-observable-interface.ps1) — the CI gate that fails on any undeclared change to routes, schema, events, SignalR, or DI registrations.

## Invariants

- The full-system acceptance suite currently has a real, acknowledged gap, stated plainly in [docs/known-issues/refactor-baseline.md](../known-issues/refactor-baseline.md)'s own "Verified local baseline" table: acceptance tests pass "but the fixture disables RabbitMQ, runs no Clinical Knowledge workers, and does not exercise the Transcription Worker or browser." Green here is not proof the whole system works end-to-end — treat it as what it verifies, not more.
- Architecture tests exist to fail *before* review, not to describe an aspiration — every rule in `Architecture.Tests`/`ModuleBoundaryTests` was written against the real, already-verified dependency graph (R38), not an ideal one.

## Dependencies

**Owns**: full-system journeys, test harnesses, baseline snapshots, dependency enforcement.
**Does not own**: implementing any feature — a red test here means something else broke, not that this module did.

## Tests

This module *is* the test suite — see "Public interface / seam" above. Backend: `dotnet test tests/MedicalAssistant.AcceptanceTests`. Frontend: `npm run test:browser` (Playwright) from `frontend/`.

## Runbook & known risks

No dedicated runbook — this is "Quality strategy" per the Ownership Map, not yet written as its own document; the CI workflows themselves (`.github/workflows/{full-system-acceptance,observable-interface}.yml`) are the closest thing today. Known risks tracked in [docs/known-issues/refactor-baseline.md](../known-issues/refactor-baseline.md):

| ID | Finding |
|---|---|
| K12 | No automated test protects the complete product path through RabbitMQ, Worker, Clinical Knowledge, and browser. |
| Q01 | Repository-root helpers treat only a `.git` directory as valid and fail in Git worktrees. |
| Q02 | A Clinical Knowledge guardrail test compares platform line endings exactly. |
