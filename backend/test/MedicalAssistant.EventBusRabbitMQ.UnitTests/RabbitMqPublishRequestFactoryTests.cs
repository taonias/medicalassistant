using System.Diagnostics;
using System.Text;
using MedicalAssistant.EventBus;
using MedicalAssistant.EventBus.Contracts;
using MedicalAssistant.EventBusRabbitMQ;

namespace MedicalAssistant.EventBusRabbitMQ.UnitTests;

public class RabbitMqPublishRequestFactoryTests
{
    [Fact]
    public void Publish_request_uses_stable_routing_key_and_persistent_message_metadata()
    {
        var envelope = IntegrationEventEnvelope.Create(
            new ConsultationTranscriptReadyV1(11, "file-11", 22, 3, "el-GR"),
            ConsultationIntegrationEvents.Registry.Resolve<ConsultationTranscriptReadyV1>(),
            "medicalassistant.backend",
            "corr-11",
            "cause-11");

        var request = RabbitMqPublishRequestFactory.Create(
            envelope,
            new RabbitMqPublishOptions());

        Assert.Equal("medicalassistant.events", request.ExchangeName);
        Assert.Equal(ConsultationIntegrationEvents.TranscriptReadyV1, request.RoutingKey);
        Assert.True(request.Mandatory);
        Assert.True(request.Properties.Persistent);
        Assert.Equal(envelope.EventId.ToString("N"), request.Properties.MessageId);
        Assert.Equal(envelope.CorrelationId, request.Properties.CorrelationId);
        Assert.Equal(envelope.EventType, request.Properties.Type);
        Assert.Equal("application/json", request.Properties.ContentType);
    }

    [Fact]
    public void Publish_request_propagates_w3c_trace_headers_without_copying_clinical_text()
    {
        using var activity = new Activity("publish-test");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.Start();

        var envelope = IntegrationEventEnvelope.Create(
            new ConsultationTranscriptReadyV1(11, "file-11", 22, 3, "el-GR"),
            ConsultationIntegrationEvents.Registry.Resolve<ConsultationTranscriptReadyV1>(),
            "medicalassistant.backend",
            "corr-11",
            "cause-11");

        var request = RabbitMqPublishRequestFactory.Create(
            envelope,
            new RabbitMqPublishOptions());
        var json = Encoding.UTF8.GetString(request.Body.Span);

        Assert.NotNull(request.Properties.Headers);
        Assert.True(request.Properties.Headers.ContainsKey("traceparent"));
        Assert.DoesNotContain("transcriptText", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patientId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("doctorId", json, StringComparison.OrdinalIgnoreCase);
    }
}
