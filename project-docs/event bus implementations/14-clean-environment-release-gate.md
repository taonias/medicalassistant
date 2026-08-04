# Clean-Environment Release Gate

T37 is the final release gate for the event-bus replacement. Its purpose is to prove the repository can be handed over as an operable, recoverable product capability from empty infrastructure, with no Azure Function dependency and no hidden legacy messaging path.

Run this gate only in a disposable environment. The clean-start commands intentionally remove local Compose volumes; do not run them against any shared developer, staging, or production data.

## Stop release policy

Stop release immediately if any of these are discovered:

- a deployed Azure Function, Function App resource, Functions package, trigger attribute, `host.json`, Functions setting, or Functions-specific deployment step is still required;
- durable messages, production history, or user data exist in a legacy direct queue;
- the backend API or Transcription Worker applies database migrations on startup instead of the dedicated migration job;
- event-bus payloads, logs, dashboards, alerts, DLQs, backups, or release evidence expose audio, transcript text, filenames, Blob URIs, provider response bodies, secrets, or raw exception text;
- backup/restore, DLQ replay, or stale/deleted Consultation reconciliation cannot be explained and rehearsed in a disposable environment;
- the release evidence has any failed, skipped-without-owner, or unknown critical gate.

## Disposable clean-start procedure

1. Copy `compose.env.example` to an ignored local environment file and replace only local disposable secrets.
2. Remove previous disposable state:

   ```powershell
   docker compose -f docker-compose.yml --env-file .env down --volumes --remove-orphans
   ```

3. Validate the root Compose model before starting containers:

   ```powershell
   docker compose -f docker-compose.yml --env-file .env config
   ```

4. Start from empty volumes:

   ```powershell
   docker compose -f docker-compose.yml --env-file .env up --build --wait
   ```

5. Confirm these root Compose deployment units are present and healthy or complete:

   - `postgres-app`
   - `postgres-clinical`
   - `rabbitmq`
   - `azurite`
   - `backend-migrations`
   - `rabbitmq-provisioner`
   - `backend-api`
   - `transcription-worker`
   - `clinical-knowledge`

6. Confirm `backend-migrations` completes successfully before `backend-api` and `transcription-worker` accept work.
7. Confirm the backend readiness endpoint reports ready after RabbitMQ topology and subscriber configuration are valid.
8. Confirm the Transcription Worker readiness check accepts only the configured audio subscriber queue and does not depend on Azure Functions tooling.

If the local Docker version does not support `--wait`, use the same Compose file and inspect container health/completion explicitly with `docker compose ps`.

## Automated repository gate

Run the backend checks from the repository root:

```powershell
dotnet test backend/MedicalAssistant.slnx --no-restore
dotnet build backend/MedicalAssistant.slnx --no-restore
```

The release evidence must include the focused guardrails as named checks:

- `ArchitecturePrivacyEnforcementTests`
- `FailureMatrixCoverageTests`
- `LegacyRemovalCompletionTests`
- `CleanEnvironmentReleaseGateTests`

These prove the active source/config rejects Azure Functions artifacts, legacy direct queues, unsafe event routing, unsafe telemetry terms, host-owned migrations, missing failure-matrix evidence, missing legacy-removal evidence, and missing release-gate documentation.

## Repository no-Functions scan

Run a no-Functions scan over active source/config:

```powershell
rg -n "Microsoft\.Azure\.Functions|ConfigureFunctionsWorkerDefaults|\[Function(?:Name)?\]|\[RabbitMQTrigger\]|AzureWebJobs|FUNCTIONS_WORKER_RUNTIME|consultation\.(processing|transcript)" backend/src AI/src docker-compose.yml compose.env.example
```

Expected result: no active source/config matches. Historical project documentation may still mention Functions or legacy queues as rejected design context.

## End-to-end release evidence

Record one release evidence entry per scenario in `12-end-to-end-failure-matrix.md`:

| Evidence field | Required content |
| --- | --- |
| Scenario | E2E ID and name |
| Environment | disposable environment identifier and commit SHA |
| Command or drill | test command, Compose operation, incident drill, or manual review performed |
| Result | pass/fail with timestamp |
| Safe identifiers | event ID, correlation ID, queue name, or service name only |
| Owner | reviewer/operator responsible for accepting the result |
| Follow-up | linked issue or explicit "none" |

Never copy clinical content, payload JSON, transcript text, filenames, Blob URIs, provider response bodies, secrets, or raw exception text into release evidence.

## Security and privacy review

The release reviewer verifies:

- RabbitMQ uses separate least-privilege identities for backend publication/consumption, worker consumption/publication, and operators.
- Database, broker, Blob Storage, Speech, Clinical Knowledge, and JWT secrets are injected from environment/secret store and are not committed.
- TLS, private networking, and management UI restrictions are represented in the target deployment plan.
- Telemetry dimensions remain content-free and match the allowlist in `10-security-and-privacy.md`.
- DLQ replay requires operator identity, reason code, immutable original event ID, and no replacement payload.
- Retention windows keep tombstones longer than outbox, inbox, DLQ, and cleanup records.

## Operations review

Before release, the operator confirms the dashboard and alert set covers:

- outbox oldest unpublished age, failed publish attempts, and expired leases;
- main, retry, and DLQ depth/oldest age per subscriber;
- consumer readiness, reconnect loops, delivery outcomes, and acknowledgement latency;
- transcription success/failure/latency and Azure Speech throttling;
- duplicate and stale-event no-op rates;
- Transcript Ready to Clinical Knowledge handoff latency and reconciliation failures;
- deletion cleanup pending age/failures;
- PostgreSQL connection/transaction/lock health and RabbitMQ memory/disk alarms.

The operations review must also rehearse the runbook paths for RabbitMQ outage, stalled outbox, Speech outage/backlog, DLQ replay, deletion cleanup, worker crash loop, credential rotation, capacity/backpressure, and disaster recovery.

## Backup and restore rehearsal

In a disposable environment:

1. Create a database backup after representative outbox, inbox, transcript, deletion tombstone, and cleanup records exist.
2. Export RabbitMQ definitions, policies, users, permissions, exchanges, queues, and bindings.
3. Destroy the disposable environment volumes.
4. Restore the database and broker definitions into fresh volumes.
5. Start `backend-migrations`, `rabbitmq-provisioner`, `backend-api`, `transcription-worker`, and `clinical-knowledge`.
6. Confirm unpublished outbox records can repopulate missing broker deliveries.
7. Confirm duplicates converge through inbox idempotency and stale/deleted Consultation state gates.
8. Confirm DLQ replay still requires the approved immutable replay seam.

Queue contents are not treated as the source of truth. Recovery is accepted only when database state, broker topology, outbox publication, inbox completion, transcript status, Clinical Knowledge handoff, deletion cleanup, and DLQ status reconcile.

## Release decision

Release is acceptable only when:

- clean root Compose starts from empty volumes;
- migrations are owned by `backend-migrations`;
- automated tests and build pass;
- the no-Functions scan has no active source/config findings;
- failure-matrix evidence is complete;
- security, privacy, dashboards, alerts, backup, restore, DLQ replay, and operations runbook reviews are signed off;
- no deployed legacy state is discovered.

If any condition fails, stop release, keep the branch unreleased, and create a follow-up issue with the failed evidence and owner.
