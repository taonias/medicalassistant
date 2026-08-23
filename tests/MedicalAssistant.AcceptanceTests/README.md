# Consultation Processing acceptance gate

This project is the public-interface acceptance gate for Consultation Processing.
Scenarios cross product interfaces only:

- a Doctor registers, authenticates, creates a Consultation, and uploads a synthetic Recording through the backend HTTP interface;
- Consultation and Transcript outcomes are read through backend HTTP endpoints;
- Clinical Knowledge is hosted as a separate process against pgvector; Ingestion submission and status are observed through its authenticated HTTP endpoint;
- PostgreSQL/pgvector, RabbitMQ, and Azurite use the same major images as the root Compose stack in real disposable containers;
- the full-system path runs the production Transcription Worker and Clinical Knowledge hosts as separate processes;
- speech and model calls cross a local OpenAI-compatible HTTP boundary whose responses contain synthetic data only;
- broker and service stop/start plus blocked Speech responses create deterministic outage and crash/race windows.

The lightweight scenarios keep provider adapters in memory while characterizing an
individual HTTP boundary. The R06 full-system scenarios enable the transactional
outbox and run this complete path:

`Recording -> Consultation Audio Uploaded -> outbox -> RabbitMQ -> Transcription Worker -> Transcript Ready -> backend consumer -> Clinical Knowledge Ingestion -> completed Document`

The fast R06 scenarios run once from clean disposable infrastructure and once after
restarting the application and infrastructure containers without deleting their
data. The backend remains hosted in-process so the Doctor interface is cheap to
drive; every asynchronous production boundary is real, and only the external
speech/model providers are controlled.

`RootComposeConsultationProcessingTests` is the packaged-image deployment gate. It
starts the repository's root `docker-compose.yml` plus a small acceptance override,
executes the same path from empty named volumes, recreates the stack without deleting
those volumes, and executes it again. The override changes only external-provider
settings, the Ingestion worker count, relay timing, and telemetry export. Service
builds, networks, dependency conditions, broker provisioning, and migrations remain
rooted in the production Compose definition.

Run the suite from the repository root:

```powershell
$env:TESTCONTAINERS_RYUK_DISABLED = "true"
dotnet test tests/MedicalAssistant.AcceptanceTests/MedicalAssistant.AcceptanceTests.csproj
```

Run only the full asynchronous gate:

```powershell
$env:TESTCONTAINERS_RYUK_DISABLED = "true"
dotnet test tests/MedicalAssistant.AcceptanceTests/MedicalAssistant.AcceptanceTests.csproj --filter FullyQualifiedName~FullSystemAcceptanceTests
```

Run the slower root Compose packaged-image gate:

```powershell
$env:RUN_ROOT_COMPOSE_GATE = "true"
dotnet test tests/MedicalAssistant.AcceptanceTests/MedicalAssistant.AcceptanceTests.csproj --filter FullyQualifiedName~RootComposeConsultationProcessingTests
```

`TESTCONTAINERS_RYUK_DISABLED` is used on this Windows development environment because the optional cleanup sidecar can be blocked while direct container cleanup still runs in `DisposeAsync`.
