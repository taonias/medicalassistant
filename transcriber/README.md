# MedicalAssistant.Transcriber

.NET 8 **isolated** Azure Function App that consumes `consultation.processing` from RabbitMQ, downloads the consultation file from Azure Blob Storage, writes an audit row to `AuditLogs`, then acknowledges (removes) the queue message.

Open `MedicalAssistant.Transcriber.slnx` in Visual Studio / Cursor to build and debug this Function App (includes a reference to `MedicalAssistant.Domain`).

## Local run

Prerequisites:
- .NET 8 SDK
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) v4
- RabbitMQ running (`rabbitmq/docker-compose.yml`)
- PostgreSQL with MedicalAssistant schema
- Blob storage reachable with the configured connection string
- Azure Speech resource (key + region) for audio transcription via fast transcription REST
  (WebM recordings are not reliable with SpeechRecognizer compressed streams without GStreamer;
  the REST API avoids that hang and RabbitMQ redelivery)
- `AzureWebJobsStorage` — Azurite (`UseDevelopmentStorage=true`) or a real storage account (Functions host still needs this)

```powershell
cd transcriber
copy local.settings.json.example local.settings.json   # if needed
dotnet build MedicalAssistant.Transcriber.slnx
dotnet run
# or F5 with the launch profile, or:
func start
```

Configuration order (same pattern as MatchResultsCollector):
1. `appsettings.json` — non-secret defaults (published)
2. Environment / `local.settings.json` `Values` — overrides for local (never published)
3. Azure App Settings — overrides when deployed

## Azure deployment

1. Create a Function App (`.NET 8 Isolated`, Windows or Linux).
2. Set application settings (same keys as `local.settings.json` `Values`):
   - `RabbitMqConnection` — e.g. `amqps://user:pass@host:5671/`
   - `RabbitMqQueueName` — `consultation.processing`
   - `RabbitMqConsultationTranscriptQueueName` — `consultation.transcript` (outbound for LLM workers)
   - `BlobStorage__ConnectionString`
   - `BlobStorage__ConsultationAudioContainer` / `BlobStorage__ConsultationDocumentsContainer`
   - `AzureSpeech__Key` — Speech resource subscription key
   - `AzureSpeech__Region` — e.g. `westeurope`
   - `AzureSpeech__Locale` — recognition locale for transcription (speech-to-text), e.g. `el-GR` (not translation)
   - `AzureSpeech__Phrases` — optional comma-separated phrase list to bias recognition (fast transcription)
   - `ConnectionStrings__MedicalAssistantDatabasePostgreSQL`
   - `AzureWebJobsStorage`
3. Publish from this project (not the backend solution):

```powershell
cd transcriber
dotnet publish -c Release
func azure functionapp publish <your-function-app-name>
```

Or from Visual Studio / zip deploy the publish output. `local.settings.json` is never included in publish; `host.json` and `appsettings.json` are.

## Behaviour

| Step | Detail |
|------|--------|
| Trigger | RabbitMQ queue message (JSON from API publisher) |
| Retrieve | Download blob using `blobUri` from the message |
| Audit | Step-by-step `AuditLogs` from process start → blob → speech → transcript → completion (or failure) |
| Transcript | Audio: Azure Speech **fast transcription** → `Transcripts`; PDF: placeholder; set consultation to `Transcribed`. Skips speech if already transcribed (RabbitMQ redelivery). |
| Outbound | After save (and on already-transcribed redelivery), publish `{ transcriptId, consultationId, correlationId, … }` to `consultation.transcript` for a future LLM Function App. |
| Ack | Host removes the inbound `consultation.processing` message on successful completion (including outbound publish). |

Failures throw so the message is not acknowledged and can be retried.
