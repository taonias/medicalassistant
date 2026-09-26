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
| POST | `/api/auth/register` | Register doctor (pending admin approval, no JWT) |
| POST | `/api/auth/login` | JWT login (rejected until the account is enabled) |
| GET | `/api/users` | Paged user list (Administrator; `page`, `pageSize`) |
| PUT | `/api/users/{id}/approval` | Enable or disable a user (Administrator) |
| GET | `/api/logs/audit` | Paged audit logs (Administrator; `page`, `pageSize`) |
| GET | `/api/logs/errors` | Paged error logs (Administrator; `page`, `pageSize`) |
| POST | `/api/patient` | Create patient |
| GET | `/api/patient/{id}/history` | Patient history |
| POST | `/api/consultation` | Create consultation |
| POST | `/api/consultation/{id}/audio` | Upload audio |
| POST | `/api/chat/query` | Chat with AI context |

## Configuration

Edit `src/MedicalAssistant.Api/appsettings.json` for connection strings, JWT, Blob Storage, and AI module URLs.

In Development, `appsettings.Development.json` seeds an administrator (`AdminSeed`: username `admin`, email `admin@localhost`, password `Admin1234`) with the Administrator and Doctor roles. New self-registrations stay disabled until this admin enables them in Settings. Leave `AdminSeed` unset in production unless you intend to seed an admin there.

Identity migrations run on API startup (`IdentityDbInitializer`). After pulling this change, restart the API so `IsApproved` is added to `AspNetUsers`.
