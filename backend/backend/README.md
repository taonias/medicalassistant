# Medical Assistant Backend

.NET 8 CQRS + Clean Architecture backend for the medical assistant system.

## Structure

```
backend/
├── src/
│   ├── MedicalAssistant.Domain/
│   ├── MedicalAssistant.Application/
│   ├── MedicalAssistant.Persistence/
│   ├── MedicalAssistant.Infrastructure/
│   ├── MedicalAssistant.Identity/
│   └── MedicalAssistant.Api/
├── test/
├── docs/
│   └── AI_MODULE_API.md
└── MedicalAssistant.slnx
```

## Prerequisites

- .NET 8 SDK
- PostgreSQL (or SQL Server via `Database:Provider`)
- Azure Blob Storage (or Azurite with `UseDevelopmentStorage=true`)
- Python AI Module (see `docs/AI_MODULE_API.md`)

## Run

```bash
cd backend
dotnet ef database update --project src/MedicalAssistant.Persistence --startup-project src/MedicalAssistant.Api
dotnet run --project src/MedicalAssistant.Api
```

Swagger: `https://localhost:7001/swagger`

## Key endpoints

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/auth/register` | Register doctor |
| POST | `/api/auth/login` | JWT login |
| POST | `/api/patient` | Create patient |
| GET | `/api/patient/{id}/history` | Patient history |
| POST | `/api/consultation` | Create consultation |
| POST | `/api/consultation/{id}/audio` | Upload audio |
| POST | `/api/chat/query` | Chat with AI context |
| POST | `/api/action/trigger` | Trigger AI action |
| POST | `/api/ai-callback/*` | AI module webhooks |

## Configuration

Edit `src/MedicalAssistant.Api/appsettings.json` for connection strings, JWT, Blob Storage, and AI module URLs.
