using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;

namespace MedicalAssistant.EventBus.UnitTests;

/// <summary>
/// The clinical-knowledge ingestion-failed event is produced by a separate codebase (the AI
/// service), so the wire shape is the contract. This pins it: the JSON below is exactly what
/// that service writes to its outbox, and it must keep deserializing into the backend's
/// registered payload. If it stops, the two sides have drifted.
/// </summary>
public class IngestionFailedContractTests
{
    private const string ClinicalKnowledgeEnvelopeJson = """
        {
          "eventId": "11111111-2222-3333-4444-555555555555",
          "eventType": "clinicalknowledge.ingestion-failed.v1",
          "eventVersion": 1,
          "occurredAtUtc": "2026-08-19T00:00:00Z",
          "producer": "clinical-knowledge",
          "correlationId": null,
          "causationId": null,
          "payload": {
            "sessionId": "42",
            "ingestionId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            "reason": "Chunking agent produced an invalid chunk plan"
          }
        }
        """;

    [Fact]
    public void Backend_deserializes_the_clinical_knowledge_ingestion_failed_envelope()
    {
        var descriptor = ConsultationIntegrationEvents.Registry.Resolve<ConsultationIngestionFailedV1>();
        Assert.Equal(ConsultationIntegrationEvents.IngestionFailedV1, descriptor.EventType);
        Assert.Equal(1, descriptor.EventVersion);

        var envelope = (IntegrationEventEnvelope<ConsultationIngestionFailedV1>)
            IntegrationEventSerializer.Deserialize(ClinicalKnowledgeEnvelopeJson, descriptor);

        Assert.Equal("clinicalknowledge.ingestion-failed.v1", envelope.EventType);
        Assert.Equal("clinical-knowledge", envelope.Producer);
        Assert.Equal("42", envelope.Payload.SessionId);
        Assert.Equal(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), envelope.Payload.IngestionId);
        Assert.Equal("Chunking agent produced an invalid chunk plan", envelope.Payload.Reason);
    }
}
