---
status: accepted
date: 2026-08-02
---

# Deploy normal containers and keep Aspire optional

The Main Backend, Transcription Worker, and Clinical Knowledge service will be packaged as ordinary containers. A root Compose definition will orchestrate the full local and integration environment, extending the repository's current RabbitMQ-only Compose setup. Production runs the API and worker as independently deployable/scalable workloads on the approved container platform. .NET Aspire may be added for developer convenience later, but it is not a runtime or architectural dependency.

We rejected Azure Functions hosting because the target explicitly removes Functions from the system. We rejected requiring Aspire as part of this event-bus change because the repository has no AppHost today and the eShop event-bus structure can be adopted without importing its complete orchestration stack.

## Consequences

- The Transcription Worker uses the standard .NET Generic Host and exposes no public product API; only protected internal health/metrics endpoints are permitted.
- Root Compose supplies RabbitMQ, database, local blob dependency/emulator where applicable, backend, worker, Clinical Knowledge service, and required networks/volumes for repeatable integration testing.
- Production configuration uses environment/secret-store injection, TLS, workload identities where supported, durable broker/database volumes, and managed backups.
- API replicas and worker replicas scale independently. Worker concurrency also respects Azure Speech quota, database capacity, memory use, and RabbitMQ prefetch.
- Startup ordering is not treated as availability: every process tolerates dependencies starting late and reconnects with bounded backoff.
- Functions packages, attributes, `host.json`, local Functions settings, and Functions deployment resources are removed after migration is proven.
