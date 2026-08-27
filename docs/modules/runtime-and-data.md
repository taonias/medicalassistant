# Runtime & Data

## Purpose

Owns composition, databases, migrations, observability, releases, and runtime/secret policy — the platform every deployable runs on, not any deployable's own feature behavior.

## Public interface / seam

`ops/{postgres,rabbitmq,deploy}` (R34), `scripts/{dev,release}` (R34), backend migrations at [backend/src/MedicalAssistant.Migrations/](../../backend/src/MedicalAssistant.Migrations/), Clinical Knowledge migrations at [clinical-knowledge/src/MedicalAssistance.Ingestion.Api/Ingestions/Migrations/](../../clinical-knowledge/src/MedicalAssistance.Ingestion.Api/Ingestions/Migrations/). Release packaging: [scripts/release/package-release.ps1](../../scripts/release/package-release.ps1) and its checks (R35).

## Invariants

- Root Compose is the local cross-service orchestration contract — see [docs/architecture/system-map.md](../architecture/system-map.md)'s "Hosting facts that refactors must preserve".
- Production deployment reads `ops/deploy/.env.prod`; the root `.env.example` is not the production contract.
- A single deployment migration job owns schema upgrades — neither the backend API nor the Transcription Worker automatically runs migrations on startup (ADR-0007).
- Release packaging rebuilds from an empty staging directory and validates a manifest (R35) — no stale files or secrets carried forward silently.

## Dependencies

**Owns**: composition, databases, migrations, observability, releases, VM/secret/runtime policy.
**Does not own**: feature behavior — this module's job is to run what every other module builds, not to decide what it does.

## Tests

Compose resolution (`docker-compose.yml`/`docker-compose.prod.yml`), migration application via `MedicalAssistant.Persistence.IntegrationTests`, and `scripts/dev/verify-observable-interface.ps1` (the CI-gated snapshot check for routes/schema/events/Compose/DI registrations — [.github/workflows/observable-interface.yml](../../.github/workflows/observable-interface.yml)).

## Runbook & known risks

Runbook: [ops/deploy/DEPLOYMENT.md](../../ops/deploy/DEPLOYMENT.md); RabbitMQ provisioning: [ops/rabbitmq/](../../ops/rabbitmq/). See also [docs/runbooks/README.md](../runbooks/README.md).

Known risks tracked in [docs/known-issues/refactor-baseline.md](../known-issues/refactor-baseline.md):

| ID | Finding |
|---|---|
| K01 | Production database initialization mounts a local-only script that creates/changes a known-password PostgreSQL superuser. |
| K10 | Release packaging is non-deterministic and can include stale files or secrets. — **resolved, R35** |
| K11 | release.info is not consumed by deployment, so packaged and deployed image tags can diverge. |
| K13 | AutoMapper and SSH.NET restore with high-severity NU1903 advisories. |
| K20 | Standalone and root RabbitMQ Compose paths can conflict on container name/ports. |
| K21 | The local start script prints database credentials. |
| K22 | The vector database UI image is mutable/unpinned. |
| K24 | Swagger appears enabled without a production environment guard. |
