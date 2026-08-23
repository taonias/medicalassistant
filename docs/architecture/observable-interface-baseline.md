# Observable Interface Baseline

Status: protected contract for behavior-preserving refactors

Verified against source at commit: ed2e38e

Baseline date: 23 August 2026

This inventory records behavior that folder and namespace refactors must not accidentally change. A deliberate contract change requires its own issue, tests, migration/compatibility plan, and release note.

The machine-readable [observable-interface snapshot](contracts/observable-interface.snapshot.json) is checked by [verify-observable-interface.ps1](../../scripts/verify-observable-interface.ps1) in pull requests. It freezes controller routes and endpoint-specific status signatures, root-solution membership, Compose services/volumes, EF model hashes, migration filenames, Integration Event envelope/payload JSON and routing keys, SignalR names, configuration paths, DI lifetimes, and critical host registrations.

## Change protocol

For every refactor slice:

1. Capture or add characterization tests around the affected contract.
2. Move one capability without mixing bug fixes.
3. Compare routes, event contracts, model snapshots, configuration, service registration, and Compose output with this baseline.
4. Keep temporary forwarding types or adapters if consumers cannot move atomically.
5. Record any pre-existing failure in the risk baseline rather than normalizing it as refactor fallout.

## Backend HTTP and real-time surface

All controller routes below are relative to the backend host.

| Area | Method and route |
|---|---|
| Action | POST /api/Action/trigger; GET /api/Action/{correlationId} |
| AI callback compatibility | POST /api/ai-callback/transcription; POST /api/ai-callback/structured-data; POST /api/ai-callback/action; POST /api/ai-callback/chat-progress |
| Authentication | POST /api/Auth/login; POST /api/Auth/register; GET /api/Auth/session; PUT /api/Auth/profile; PUT /api/Auth/password |
| Chat | POST /api/Chat/ask; POST /api/Chat/query |
| Consultations | GET /api/Consultation/drafts; GET /api/Consultation/analytics; GET /api/Consultation/drafts/unattached; GET /api/Consultation/patient/{patientId}; GET /api/Consultation/{id}; POST /api/Consultation; PUT /api/Consultation/{id}/patient; DELETE /api/Consultation/{id} |
| Consultation files/workflow | POST /api/Consultation/{id}/audio; POST /api/Consultation/{id}/document; POST /api/Consultation/{id}/retry; GET /api/Consultation/{id}/audio; GET /api/Consultation/{id}/document; GET /api/Consultation/{id}/structured-data; POST /api/Consultation/{id}/structured-data/approve |
| Conversations | POST /api/Conversations; GET /api/patients/{patientId}/conversations; GET /api/Conversations/{id}/messages; PATCH /api/Conversations/{id}; DELETE /api/Conversations/{id}; POST /api/Conversations/{id}/messages/{messageId}/retry |
| Doctor notes | GET /api/DoctorNotes/consultations/{consultationId}; GET /api/DoctorNotes/patients/{patientId}; POST /api/DoctorNotes |
| Patients | GET /api/Patient; GET /api/Patient/{id}; GET /api/Patient/{id}/history; POST /api/Patient; PUT /api/Patient |
| Transcript | GET /api/Transcript/{consultationId}; PUT /api/Transcript/{consultationId} |
| Health | GET /health/live; GET /health/ready |
| SignalR | /hubs/chat |

Controller casing is captured because existing clients may depend on it even where routing is nominally case-insensitive.

### Backend declared status behavior

| Status | Endpoints/conditions |
|---|---|
| 200 OK | Reads and updates; login/register; callbacks with a valid callback key; Consultation uploads/retry; POST /api/Consultation whenever a nonblank idempotency key is supplied; chat/conversation operations; Doctor Note creation |
| 201 Created | POST /api/Patient; POST /api/Consultation when no nonblank idempotency key is supplied |
| 202 Accepted | POST /api/Action/trigger |
| 204 No Content | DELETE Consultation; DELETE Conversation; PUT password; approve Structured Medical Data |
| 206 Partial Content | Consultation audio download can serve range requests |
| 401 Unauthorized | Protected endpoints/auth middleware; session/profile/password without a valid Doctor; callbacks with an invalid key |
| 404 Not Found | Consultation audio/document when no object exists; domain exception middleware can produce other not-found responses |

The backend does not annotate every error response in OpenAPI. Validation, authorization, and exception middleware can add 400/401/403/404/409/500 responses. R07 will characterize exact error bodies; structural work must not normalize them first.

## Clinical Knowledge HTTP and real-time surface

| Area | Method and route |
|---|---|
| Ingestion | POST /ingestions; GET /ingestions; GET /ingestions/{id}; GET /ingestions/{id}/quality; POST /ingestions/{id}/retry |
| Documents | DELETE /documents/{documentId} |
| Patient knowledge | GET /patients/{patientId}/documents; GET /patients/{patientId}/summary; DELETE /patients/{patientId}/data |
| Patient chat | POST /patients/{patientId}/chat/answer; POST /patients/{patientId}/chat/summarize |
| SignalR | /hubs/ingestion-status |

### Clinical Knowledge declared status behavior

| Endpoint | Declared statuses |
|---|---|
| POST /ingestions | 202, 400, 409 |
| GET /ingestions | 200, 400 |
| GET /ingestions/{id}; GET /ingestions/{id}/quality | 200, 404 |
| POST /ingestions/{id}/retry | 202, 404, 409 |
| DELETE /documents/{documentId} | 200, 400, 404, 409 |
| GET /patients/{patientId}/documents | 200 |
| GET /patients/{patientId}/summary | 200, 404 |
| DELETE /patients/{patientId}/data | 200, 400, 401, 403 |
| POST /patients/{patientId}/chat/answer | 200, 400, 401, 500 |
| POST /patients/{patientId}/chat/summarize | 200, 401 |

## OpenAPI and SignalR

| Host | OpenAPI JSON/UI | Security declaration |
|---|---|---|
| Backend API | /swagger/v1/swagger.json and /swagger | Bearer JWT in Authorization header |
| Clinical Knowledge | /swagger/v1/swagger.json and /swagger | ApiKey scheme in X-Api-Key header |

Both hosts currently map Swagger without an environment guard; K24 records this separately.

| Hub | Direction | Client method/event | Payload |
|---|---|---|---|
| /hubs/chat | Backend → Doctor browser | ChatProgress | Chat progress contract sent to Clients.User(doctorId) |
| /hubs/ingestion-status | Clinical Knowledge → subscribers | IngestionStatusChanged | ingestionId, documentId, sessionId, doctorId, patientId, stage, errorMessage, occurredAt |

Neither hub defines a client-invoked method. Ingestion stages are Queued, Chunking, Embedding, Storing, Completed, and Failed.

## Integration-event envelope

Every event uses these envelope fields:

| Field | Meaning |
|---|---|
| eventId | Idempotency identity |
| eventType | Versioned routing identity |
| eventVersion | Schema version |
| occurredAtUtc | Producer timestamp |
| producer | Originating deployable/context |
| correlationId | End-to-end workflow identity |
| causationId | Event/command that caused this event |
| payload | Event-specific identifiers and facts |

## Event catalog

| Event | Routing key | Version | Payload fields |
|---|---|---:|---|
| Consultation Audio Uploaded | consultation.audio-uploaded.v1 | 1 | consultationId, fileId, contentType, storageObjectReference, durationSeconds |
| Consultation Document Uploaded | consultation.document-uploaded.v1 | 1 | consultationId, fileId, documentType, contentType, storageObjectReference |
| Transcript Ready | consultation.transcript-ready.v1 | 1 | consultationId, fileId, transcriptId, transcriptRevision, languageCode |
| Transcription Failed | consultation.transcription-failed.v1 | 1 | consultationId, fileId, failureCode, failureCategory |
| Consultation Deleted | consultation.deleted.v1 | 1 | consultationId, deletedAtUtc, reasonCode |
| Clinical Knowledge Ingestion Failed | clinicalknowledge.ingestion-failed.v1 | 1 | sessionId, ingestionId, reason |

The RabbitMQ exchange is medicalassistant.events. Current retry-delay configuration is empty even though Consultation Processing ADR 0003 describes five retries. Preserve current runtime behavior during structural refactors and address the discrepancy as a separate defect/configuration change.

### Producers and consumers

| Producer | Publishes | Consumer |
|---|---|---|
| Backend API | Consultation Audio Uploaded, Consultation Document Uploaded, Consultation Deleted | Transcription Worker or downstream processors |
| Transcription Worker | Transcript Ready, Transcription Failed | Backend API |
| Clinical Knowledge | Clinical Knowledge Ingestion Failed | Backend API |

Transcript content must remain out of RabbitMQ messages. The receiving service loads it from the owning context through the existing integration boundary.

## Persistence compatibility markers

The following EF model snapshots are change detectors, not manually edited contracts. Hashes are SHA-256 after normalizing line endings to LF so the check is cross-platform:

| Persistence area | Snapshot SHA-256 |
|---|---|
| Clinical Knowledge | DB01996EF1783AE8ECBF7FCC34AE086BCEFA36502E6CCF8F33F1E4BEE6290C73 |
| Backend Identity | 9E78A3569B6F3F8D708F37312AC8B915ADDC5891A682B60A878203FD3AD3F1B7 |
| Backend domain persistence | 7201A478751E8F046744BEF42753479D3015E331471FE6B1E18E434012DB3A07 |

During a folder-only refactor:

- Do not add, remove, or regenerate migrations.
- Do not change table/column names, indexes, constraints, value conversions, or provider selection.
- A snapshot hash change is a stop signal requiring investigation.
- Outbox/inbox writes must keep their existing transaction boundaries.

The committed machine snapshot also lists every non-designer EF migration filename in all three persistence areas. Adding, removing, renaming, or relocating a historical migration fails the Wave 0 check.

## Configuration compatibility

Configuration hierarchy and exact names are observable deployment contracts.

| Deployable | Exact configuration paths to preserve |
|---|---|
| Backend API | Database:Provider; ConnectionStrings:MedicalAssistantDatabasePostgreSQL; ConnectionStrings:MedicalAssistantDatabaseSqlServer; JwtSettings:{Key,Issuer,Audience,DurationInMinutes}; CorsSettings:AllowedOrigins; BlobStorage:{ConnectionString,ConsultationAudioContainer,ConsultationDocumentsContainer}; AiModule:{BaseUrl,ApiKey,ApiBaseUrl}; ClinicalKnowledge:{BaseUrl,ApiKey,SubmitTimeoutSeconds}; AiCallback:ApiKey; AppSettings:{AllowedAudioContentTypes,MaxAudioFileSizeBytes,AllowedDocumentContentTypes,MaxDocumentFileSizeBytes}; EventRetention:{OutboxRetention,InboxRetention,DeadLetterRetention,TombstoneRetention}; ConsultationOutboxRelay:{Enabled,BatchSize,LeaseDuration,PollInterval,FailureBackoff}; SeedDoctor; OTEL_EXPORTER_OTLP_ENDPOINT |
| Backend RabbitMQ | RabbitMQ:{HostName,Port,UserName,Password,VirtualHost,ClientProvidedName,UseTls}; RabbitMQ:Consumer:{QueueName,PrefetchCount,ShutdownDrainTimeout}; RabbitMQ:Topology:{ExchangeName,SubscriberName,QueueName,RetryDelays}; RabbitMQ:Publisher:{ExchangeName,ConfirmTimeout} |
| Transcription Worker | Database:Provider; ConnectionStrings:MedicalAssistantDatabasePostgreSQL; the RabbitMQ paths above; TranscriptionWorker:{Provider,ShutdownDrainTimeout,ProcessingLeaseDuration,MaxConcurrentTranscriptions}; TranscriptionWorker:BlobRetrieval:{ConnectionString,ConsultationAudioContainer,MaxBytes,AllowedContentTypes}; TranscriptionWorker:AzureSpeech:{Key,Region,LanguageCode,ApiVersion,Timeout,Phrases}; TranscriptionWorker:OpenAiWhisper:{ApiKey,Model,BaseUrl,LanguageCode,Timeout}; OTEL_EXPORTER_OTLP_ENDPOINT |
| Clinical Knowledge | ConnectionStrings:Postgres; Authentication:{ApiKeys,AdminApiKeys}; Ingestion:{WorkerCount,MaxAttempts,RecoverySweepInterval}; Chunking:{MinTokens,MaxTokens}; Retrieval:{ConfidenceThreshold,QueryRefinement:Enabled}; Extraction:MaxPdfBytes; DocumentArchive:LocalRootPath; OpenAIChat:{ApiKey,BaseUrl,Model}; OpenAIEmbeddings:{ApiKey,BaseUrl,Model}; AzureOpenAI:{Endpoint,ApiKey,ChatDeployment,EmbeddingDeployment,Embedding:Dimensions}; DocumentIntelligence:{Endpoint,ApiKey}; BackendCallback:{BaseUrl,ApiKey}; RabbitMQ:{Host,Port,VirtualHost,Username,Password,ExchangeName,ClientProvidedName}; OTEL_EXPORTER_OTLP_ENDPOINT |
| Frontend | VITE_API_BASE_URL; localStorage keys medical-assistant-auth, medical-assistant-theme, and medical-assistant:recent-patients |

Secrets must continue to come from environment/configuration providers; a refactor must not copy them into source defaults.

## Dependency injection and hosted-process baseline

| Host | Long-running responsibilities |
|---|---|
| Backend API | Consultation outbox relay, RabbitMQ transcription-outcome consumer, conversation-summary hosted service |
| Transcription Worker | RabbitMQ audio-event consumer, transcription orchestration, worker outbox relay, readiness endpoint |
| Clinical Knowledge | Ingestion worker, recovery sweep, integration-event outbox relay |

### Registration lifetime inventory

| Host/module | Singleton | Scoped/transient | Hosted/typed HTTP |
|---|---|---|---|
| Backend Application/API | conversation-summary queue; outbox metrics; replay policy; options validators; SignalR Doctor user-id provider | MediatR validation/audit behaviors are transient; Patient history, chat turn, summary refresher, replay service, and SignalR notifier are scoped | — |
| Backend Persistence | — | DbContext and all repositories, inbox/outbox stores, completion/preparation units, audit/error loggers are scoped | — |
| Backend Infrastructure | RabbitMQ connection, confirmed publisher, outbox/replay publishers, replay safety check, event registry/dispatcher | blob/PDF adapters, outbox relay, and Integration Event handlers are scoped | AI module and Clinical Knowledge typed HTTP clients; outbox relay, conversation summary, and RabbitMQ consumer hosted services |
| Transcription Worker | event registry/dispatcher and options validator | blob retrieval and Consultation Audio Uploaded handler; speech clients are typed-HTTP scoped services | RabbitMQ consumer and outbox relay; a readiness check is registered but not mapped to HTTP by the generic host |
| Clinical Knowledge | Npgsql data source; agent instructions/status publisher; provider clients; archive; channel; RabbitMQ publisher | DbContext, ingestion store/queue, strategies, retrieval pipeline, Grounded Chat, and summary services | progress typed HTTP client; ingestion worker, recovery sweep, and outbox relay |

Concrete registrations and registration-order sentinels are stored in the machine snapshot. Provider replacement order in Clinical Knowledge remains behaviorally significant.

Preserve service lifetime, registration order where it affects decoration/options, health checks, and startup validation. Extract registration extensions by capability only after characterization coverage exists.

## Root solution membership

The root MedicalAssistant.slnx is the developer entry point and contains:

- Backend API, Application, Domain, Infrastructure, Persistence, Migrations, Transcription Worker, and backend test projects.
- The Clinical Knowledge API project and its test project (the context's internal layers are folders within those projects).

Adding the active Transcription Worker to this root solution is part of Wave 0 and does not alter runtime behavior.

## Compose topology

The root Compose service names are:

- aspire-dashboard
- azurite
- postgres
- backend-migrations
- pgadmin
- vector-db-ui
- rabbitmq
- rabbitmq-provisioner
- clinical-knowledge
- backend-api
- transcription-worker

Named volumes are medicalassistant_pgdata, medicalassistant_pgadmin_data, medicalassistant_rabbitmq_data, and medicalassistant_azurite_data.

Refactors must preserve service names, networks, volumes, health checks, dependency conditions, exposed ports, and environment-variable names. Production deployment files are a separate contract and must be validated independently.

## Browser contracts

- Authentication/session bootstrap and route protection must retain current navigation behavior.
- Consultation capture retains the current draft → upload → processing → review/approval sequence.
- Chat remains backend-mediated; the browser does not call Clinical Knowledge directly.
- Existing SignalR hub paths, polling behavior, download semantics, query-cache keys, and error shapes are compatibility-sensitive.
- Component moves must not alter URLs, request bodies, response parsing, or browser storage keys.

## Verification commands

Run from the repository root:

- pwsh -NoProfile -File scripts/verify-observable-interface.ps1
- dotnet build MedicalAssistant.slnx
- dotnet test backend/MedicalAssistant.slnx
- dotnet test AI/MedicalAssistance.Ingestion.slnx
- dotnet test tests/MedicalAssistant.AcceptanceTests/MedicalAssistant.AcceptanceTests.csproj
- npm run lint and npm run build in frontend
- docker compose config --services

Known baseline failures and warnings are recorded in the [refactor risk baseline](../known-issues/refactor-baseline.md). A refactor is acceptable only when it introduces no new failures and no unreviewed contract drift.
