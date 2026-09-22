# MedicalAssistant.AiModule

.NET 8 **isolated** Azure Function App that consumes `ai.requests` from RabbitMQ and deserializes the transcript-ready message.

Open `MedicalAssistant.AiModule.slnx` in Visual Studio / Cursor to build and debug this Function App. It is independent of the transcriber and backend solutions. Use port **7072** so it can run alongside the transcriber (7071).

## Local run

Prerequisites:
- .NET 8 SDK
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) v4
- RabbitMQ running (`rabbitmq/docker-compose.yml`)
- `AzureWebJobsStorage` — Azurite (`UseDevelopmentStorage=true`) or a real storage account (Functions host still needs this)

```powershell
cd aimodule
copy local.settings.json.example local.settings.json   # if needed
dotnet build MedicalAssistant.AiModule.slnx
# F5 with the launch profile, or:
func start
```

Configuration order:
1. `appsettings.json` — non-secret defaults (published)
2. Environment / `local.settings.json` `Values` — overrides for local (never published)
3. Azure App Settings — overrides when deployed

## Azure deployment

1. Create a Function App (`.NET 8 Isolated`, Windows or Linux).
2. Set application settings (same keys as `local.settings.json` `Values`):
   - `RabbitMqConnection` — e.g. `amqps://user:pass@host:5671/`
   - `RabbitMqQueueName` — `ai.requests`
   - `AzureWebJobsStorage`
3. Publish from this project:

```powershell
cd aimodule
dotnet publish -c Release
func azure functionapp publish <your-function-app-name>
```

`local.settings.json` is never included in publish; `host.json` and `appsettings.json` are.

## Behaviour

| Step | Detail |
|------|--------|
| Trigger | RabbitMQ queue message on `ai.requests` (JSON from transcriber / API) |
| Deserialize | `TranscriptReadyMessage` (`eventType`, `transcriptId`, `consultationId`, `correlationId`, `occurredAtUtc`) |
| Ack | Host removes the inbound message on successful completion |

Failures throw so the message is not acknowledged and can be retried.
