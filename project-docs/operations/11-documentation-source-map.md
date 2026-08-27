# Documentation Source Map

> Historical note: this describes how the 31 July 2026 review (this `operations/` series) was compiled — it is not a living index. For the current documentation set, start at [docs/](../../docs/) and its module/context/runbook indexes.

## Scope and method

These operations documents were synthesized from every active Markdown file outside `project-docs` and `clinical-knowledge/docs/helping_documents_out_of_repo`, then checked against application source, project manifests, configuration examples, controllers, handlers, entities, service registration, migrations, and test inventory.

The `helping_documents_out_of_repo` tree is reference material imported from another project. It influenced the AI retrieval design but is not treated as an active deployable component.

## Existing source documents

| Source | What it contributes | Current assessment |
| --- | --- | --- |
| [Backend README](../../backend/README.md) | .NET 8 structure, prerequisites, main endpoint overview | Useful but AI dependency description is stale |
| [Legacy AI module contract](../../backend/docs/AI_MODULE_API.md) | `/v1/*` HTTP/callback contract expected by backend | Accurately describes current client, but no matching implementation exists here |
| [Frontend README](../../frontend/README.md) | Stack, routes, backend dependencies, UX notes | Mostly current; recent-patient limitation is stale |
| [Transcriber README](../../transcriber/README.md) | Function setup, queue names, speech behavior, deployment | Strong and mostly aligned with code |
| [AI glossary](../../clinical-knowledge/CONTEXT.md) | Precise ingestion/retrieval terminology | Strong; incorporated into the context map |
| [AI database guide](../../clinical-knowledge/database/README.md) | pgvector container and migration workflow | Strong; destructive reset warning is operationally important |
| [Ingestion design](../../clinical-knowledge/docs/ingestion-pipeline-design.md) | Detailed document types, strategies, state, safety, deferred work | Strong design record and largely implemented |
| [Ingestion PRD](../../clinical-knowledge/docs/prd/0001-clinical-document-ingestion-service.md) | Problem, value, user stories, constraints | Strong product intent; “chat is future” was superseded by later work |
| [Retrieval/chat design](../../clinical-knowledge/docs/retrieval-and-chat-design.md) | Retrieval/grounded-answer contract and boundary | Strong but incorrectly says backend does not exist |
| [Retrieval/chat PRD](../../clinical-knowledge/docs/prd/0002-retrieval-and-grounded-chat.md) | Grounded-chat user value and acceptance behaviors | Strong target; test counts and some verification language are stale/stronger than code |
| [AI ADRs](../../clinical-knowledge/docs/adr/) | Twelve load-bearing architecture decisions | Current and valuable; indexed in this suite |

## Code sources used to resolve truth

### Frontend

- `src/app/router.tsx` for actual routes.
- `src/layouts/navigation/navItems.tsx` for primary product areas.
- `src/features/*/api` for backend calls.
- Dashboard, patient, record, consultation, chat, and settings pages for user-visible behavior.
- `src/shared/types/api.ts` for active DTO expectations.
- Auth store and HTTP client for browser session behavior.

### Main backend

- API controllers for the actual HTTP surface and authorization attributes.
- Application feature handlers for doctor scoping, workflow behavior, best-effort integration, and state changes.
- Domain entities/enums for the care-workflow vocabulary and status model.
- Persistence/Identity registration and entity mappings for data stores and security policy.
- Infrastructure adapters for Blob Storage, RabbitMQ, and legacy AI endpoints.

### Transcriber

- RabbitMQ Function entry point.
- Transcript service for idempotency, status transitions, placeholder PDF behavior, and outgoing event.
- Azure Speech and Blob retrieval services.
- Narrow transcriber EF Core context and configuration examples.

### AI service

- Program/service registration for security, providers, telemetry, workers, retrieval, and migrations.
- Ingestion requests, strategies, store, entities, and controllers.
- Retrieval steps and SQL behavior.
- Grounded-answer orchestration, refusal, generator, and citation verifier.
- EF Core migrations to determine implemented schema/prompts.
- Test file and attribute inventory to determine coverage shape.

## Interpretation rule

Use this precedence when maintaining these documents:

1. Executed production behavior and verified tests, when available.
2. Current source code and migrations.
3. Current API/config examples.
4. README/design/PRD descriptions.
5. Imported reference documents.

Design documents still matter when code is incomplete, but target behavior must be labelled as target rather than current capability.

## Maintenance checklist

Update this suite when any of the following changes:

- Public route, DTO, queue contract, or status enum.
- Service boundary or data ownership.
- Provider/model/embedding dimension.
- Document type or ingestion strategy.
- Authentication, authorization, or erasure behavior.
- Deployment topology or configuration keys.
- Connected end-to-end workflow status.
- Major gap in [Current state and gaps](10-current-state-and-gaps.md) is closed.

The highest-maintenance pages are the capability status table, architecture diagram, interface catalog, local startup guide, and gap assessment.

