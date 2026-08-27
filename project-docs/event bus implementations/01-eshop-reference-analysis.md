# Official eShop Event Bus: Reference Analysis

## Reference inspected

Repository: [dotnet/eShop](https://github.com/dotnet/eShop)  
Commit inspected: `9b4f9434f46fdc5c1a6e9e936af2868340cdbc48` (21 April 2026)

Relevant official source:

- [`IntegrationEvent`](https://github.com/dotnet/eShop/blob/main/src/EventBus/Events/IntegrationEvent.cs)
- [`IEventBus`](https://github.com/dotnet/eShop/blob/main/src/EventBus/Abstractions/IEventBus.cs)
- [`IIntegrationEventHandler`](https://github.com/dotnet/eShop/blob/main/src/EventBus/Abstractions/IIntegrationEventHandler.cs)
- [Subscription registration](https://github.com/dotnet/eShop/blob/main/src/EventBus/Extensions/EventBusBuilderExtensions.cs)
- [RabbitMQ event bus](https://github.com/dotnet/eShop/blob/main/src/EventBusRabbitMQ/RabbitMQEventBus.cs)
- [RabbitMQ dependency injection](https://github.com/dotnet/eShop/blob/main/src/EventBusRabbitMQ/RabbitMqDependencyInjectionExtensions.cs)
- [Integration-event log](https://github.com/dotnet/eShop/tree/main/src/IntegrationEventLogEF)
- [Standalone Payment Processor subscriber](https://github.com/dotnet/eShop/blob/main/src/PaymentProcessor/Program.cs)
- [Aspire AppHost composition](https://github.com/dotnet/eShop/blob/main/src/eShop.AppHost/Program.cs)

## Pattern used by eShop

### Shared abstractions

eShop separates broker-independent contracts from RabbitMQ transport:

- `IntegrationEvent` supplies a GUID event ID and UTC creation date.
- `IEventBus` exposes asynchronous publication.
- `IIntegrationEventHandler<TEvent>` gives each event a typed handler.
- `AddSubscription<TEvent, THandler>` registers keyed transient handlers and records the mapping from event type name to CLR type.
- JSON options can be configured for source-generated serialization/AOT support.

This separation is a good fit for Medical Assistant. Domain/application code can depend on event contracts while connection, topology, serialization, and consumption remain infrastructure concerns.

### RabbitMQ topology

eShop declares one direct exchange named `eshop_event_bus`. Each subscriber application has a durable queue named by `EventBus:SubscriptionClientName`. At startup the queue is bound to the exchange once for every registered event type, using the CLR event type name as the routing key.

For Medical Assistant, the same shape means one exchange, one durable queue per independently deployable subscriber, and event-name bindings that deliver only events the service handles. It replaces today's two manually addressed queues and removes the need for queue-scanning deletion logic.

### Consumer hosting and dispatch

The RabbitMQ event bus implements `IHostedService`. Registering it starts consumption with the application. On delivery it:

1. Reads the routing key as the event name.
2. Resolves the corresponding registered event type.
3. Deserializes the payload.
4. Creates a dependency-injection scope.
5. Resolves all keyed handlers for that event type.
6. Invokes each handler.

`PaymentProcessor` demonstrates the desired standalone subscriber: it is a small deployable host that registers the event bus and one typed handler. The Medical Assistant Transcription Worker should follow this boundary.

### Publication resilience and tracing

The eShop RabbitMQ publisher:

- Publishes persistent messages to the direct exchange.
- Uses mandatory routing.
- Retries broker-unreachable/socket failures with exponential delays through Polly.
- Injects OpenTelemetry propagation headers.
- Creates publish and receive activities using messaging semantic attributes.

These are useful building blocks, but publisher confirms and unroutable-message handling must be made explicit in the Medical Assistant implementation.

### Database/event atomicity

For important ordering changes, eShop stores an `IntegrationEventLogEntry` in the same local database transaction as business data, then publishes after commit and records publishing status/attempt count. This is an integration-event-log/outbox pattern.

Medical Assistant currently saves an uploaded consultation and then performs a best-effort direct publish. A broker failure can therefore leave a stored consultation that no worker will ever process. The target design should use a durable outbox plus a background relay, rather than relying only on the request thread to publish.

## Behaviors not safe to copy unchanged

### Failed messages are acknowledged

eShop catches consumer exceptions and acknowledges the message anyway. Its own source explicitly says a real application should use a Dead Letter Exchange. Medical Assistant must instead distinguish retryable and terminal failures, negatively acknowledge or dead-letter appropriately, and never silently discard clinical work.

### Full message bodies enter logs and traces

eShop logs message content and adds the serialized message to telemetry. Medical Assistant events may contain PHI-bearing identifiers, filenames, URIs, or clinical text. Telemetry must carry event ID, type, correlation ID, consultation ID, attempt, duration, and outcome—not payload text or transcript content.

### Event naming is tied to CLR type names

Using `typeof(T).Name` as the external routing key is convenient but makes renames a wire-contract change. Medical Assistant should define stable event names and versions explicitly.

### No complete consumer retry/dead-letter policy

The sample does not provide production poison-message handling, delayed retries, dead-letter replay, or idempotency storage. Those must be part of the Medical Assistant design.

### Integration-event log is not a complete relay by itself

eShop publishes pending events after the transaction and records failures. Medical Assistant needs a continuous background relay that finds unpublished events after process crashes or broker outages, with leasing/claiming and bounded retry behavior.

## Initial mapping to Medical Assistant

| eShop concept | Medical Assistant target |
| --- | --- |
| `EventBus` project | `MedicalAssistant.EventBus` abstractions/contracts |
| `EventBusRabbitMQ` project | `MedicalAssistant.EventBusRabbitMQ` transport/topology |
| `IntegrationEventLogEF` | Main-backend transactional outbox |
| `PaymentProcessor` | Standalone `MedicalAssistant.Transcription.Worker` |
| `OrderStatusChanged...IntegrationEvent` | `ConsultationAudioUploadedV1` |
| Handler publishes result event | Worker publishes `ConsultationTranscriptReadyV1` or `ConsultationTranscriptionFailedV1` |
| AppHost RabbitMQ reference | Root deployment/orchestration definition for broker and worker |

## Conclusion

Adopt eShop's modular contracts, DI registration, typed handlers, subscriber-owned durable queues, direct-exchange routing, and trace propagation. Strengthen its sample delivery semantics with a transactional outbox, publisher confirms, idempotent consumers, bounded retry queues, dead-lettering, replay operations, stable versioned event names, and PHI-safe telemetry.
