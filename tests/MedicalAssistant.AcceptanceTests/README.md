# Consultation Processing acceptance seam

This project is the pre-Compose acceptance seam for the event-bus implementation.
Scenarios cross product interfaces only:

- a Doctor registers, authenticates, creates a Consultation, and uploads a synthetic Recording through the backend HTTP interface;
- Consultation and Transcript outcomes are read through backend HTTP endpoints;
- Clinical Knowledge is hosted as a separate process against pgvector; Ingestion submission and status are observed through its authenticated HTTP endpoint;
- PostgreSQL/pgvector and RabbitMQ are real disposable containers;
- Blob Storage and Azure Speech are controlled adapters containing synthetic data only;
- broker and service stop/start plus blocked Speech responses create deterministic outage and crash/race windows.

The T02 slice proves clean infrastructure startup, broker outage recovery, Clinical Knowledge process restart with durable Ingestion status, controlled Speech timing, and the existing HTTP upload/status path. It deliberately disables the legacy direct RabbitMQ publisher. The complete `upload -> Transcript -> Clinical Knowledge` scenario becomes green as T10-T24 add the outbox, worker, Transcript Ready consumer, and Ingestion handoff. Those tasks must extend this seam rather than test private handlers or database rows.

T31 will run these public-interface scenarios against the complete root Compose stack. Until those production services exist, this harness keeps the backend in-process while using real disposable infrastructure and an explicit migration-job helper.

Run the suite from the repository root:

```powershell
$env:TESTCONTAINERS_RYUK_DISABLED = "true"
dotnet test tests/MedicalAssistant.AcceptanceTests/MedicalAssistant.AcceptanceTests.csproj
```

`TESTCONTAINERS_RYUK_DISABLED` is used on this Windows development environment because the optional cleanup sidecar can be blocked while direct container cleanup still runs in `DisposeAsync`.
