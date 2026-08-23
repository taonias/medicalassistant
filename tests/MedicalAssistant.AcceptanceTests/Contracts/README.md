# Backend API contract gates

These tests protect the Backend API interface while its implementation is moved into clearer Care Workflow capability folders.

## What is protected

- `BackendOpenApiContractTests` starts the real HTTP host and compares its generated OpenAPI document with the committed snapshot. The top-level hash freezes the whole document; the per-operation and per-schema hashes identify the interface area that drifted.
- `BackendHttpContractTests` calls the real routing and model-binding pipeline. It protects the current multipart field names, audio range responses, document download filename, missing-file status, and all four legacy AI callback routes and their `X-Api-Key` behavior.
- `BackendRealtimeContractTests` protects `/hubs/chat`, the `ChatProgress` client method, Doctor-specific routing, and the serialized event shape.
- `BackendContractApiFactory` replaces only external infrastructure, hosted processes, and persistence-backed role seeding. Controllers, middleware, model binding, authentication, OpenAPI, and SignalR are the production implementations.

## Reviewing intentional contract changes

Run the focused gate from the repository root:

```powershell
dotnet test tests/MedicalAssistant.AcceptanceTests/MedicalAssistant.AcceptanceTests.csproj --filter FullyQualifiedName~MedicalAssistant.AcceptanceTests.Contracts
```

If OpenAPI changes, the failing test writes the proposed snapshot to the operating system temporary directory. Review the generated document and the behavior change first. Update the committed snapshot only for a separately approved contract change; folder and namespace refactors must leave it unchanged.
