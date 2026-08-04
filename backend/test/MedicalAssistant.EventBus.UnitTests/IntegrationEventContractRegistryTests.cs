using System.Text.Json;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;

namespace MedicalAssistant.EventBus.UnitTests;

public class IntegrationEventContractRegistryTests
{
    [Fact]
    public void Default_catalog_uses_explicit_stable_routing_keys_instead_of_clr_names()
    {
        var descriptor = ConsultationIntegrationEvents.Registry.Resolve<ConsultationAudioUploadedV1>();

        Assert.Equal("consultation.audio-uploaded.v1", descriptor.EventType);
        Assert.Equal(1, descriptor.EventVersion);
        Assert.NotEqual(nameof(ConsultationAudioUploadedV1), descriptor.EventType);
    }

    [Fact]
    public void Default_catalog_resolves_all_initial_consultation_processing_contracts()
    {
        string[] eventTypes =
        [
            ConsultationIntegrationEvents.AudioUploadedV1,
            ConsultationIntegrationEvents.DocumentUploadedV1,
            ConsultationIntegrationEvents.TranscriptReadyV1,
            ConsultationIntegrationEvents.TranscriptionFailedV1,
            ConsultationIntegrationEvents.DeletedV1
        ];

        foreach (var eventType in eventTypes)
        {
            var descriptor = ConsultationIntegrationEvents.Registry.Resolve(eventType, 1);

            Assert.Equal(eventType, descriptor.EventType);
            Assert.Equal(1, descriptor.EventVersion);
        }
    }

    [Fact]
    public void Registry_rejects_duplicate_clr_contracts_and_duplicate_wire_versions()
    {
        var duplicateClr = Assert.Throws<DuplicateIntegrationEventContractException>(() =>
            IntegrationEventContractRegistry.Create(builder => builder
                .Add<ConsultationAudioUploadedV1>("consultation.audio-uploaded.v1", 1)
                .Add<ConsultationAudioUploadedV1>("consultation.audio-uploaded.v2", 2)));

        Assert.Contains(nameof(ConsultationAudioUploadedV1), duplicateClr.Message);

        var duplicateWireName = Assert.Throws<DuplicateIntegrationEventContractException>(() =>
            IntegrationEventContractRegistry.Create(builder => builder
                .Add<ConsultationAudioUploadedV1>("consultation.audio-uploaded.v1", 1)
                .Add<ConsultationDocumentUploadedV1>("consultation.audio-uploaded.v1", 1)));

        Assert.Contains("consultation.audio-uploaded.v1", duplicateWireName.Message);
    }

    [Fact]
    public void Registry_rejects_unsupported_contract_versions()
    {
        Assert.Throws<UnsupportedIntegrationEventContractException>(() =>
            ConsultationIntegrationEvents.Registry.Resolve("consultation.audio-uploaded.v1", 2));

        Assert.Throws<UnsupportedIntegrationEventContractException>(() =>
            ConsultationIntegrationEvents.Registry.Resolve("consultation.unknown.v1", 1));
    }

    [Fact]
    public void Envelope_serializes_metadata_separately_from_minimal_payload()
    {
        var payload = new ConsultationTranscriptReadyV1(
            ConsultationId: 123,
            FileId: "file-456",
            TranscriptId: 789,
            TranscriptRevision: 3,
            LanguageCode: "el-GR");

        var envelope = IntegrationEventEnvelope.Create(
            payload,
            ConsultationIntegrationEvents.Registry.Resolve<ConsultationTranscriptReadyV1>(),
            producer: "medicalassistant.backend",
            correlationId: "corr-1",
            causationId: "cause-1",
            occurredAtUtc: new DateTime(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc));

        var json = JsonSerializer.Serialize(envelope);

        Assert.Contains("\"eventType\":\"consultation.transcript-ready.v1\"", json);
        Assert.Contains("\"eventVersion\":1", json);
        Assert.Contains("\"transcriptRevision\":3", json);
        Assert.DoesNotContain("transcriptText", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("doctorId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patientId", json, StringComparison.OrdinalIgnoreCase);
    }
}
