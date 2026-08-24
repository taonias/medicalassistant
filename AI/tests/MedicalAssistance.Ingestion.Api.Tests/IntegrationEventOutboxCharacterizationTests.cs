using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using MedicalAssistance.Ingestion.Api.Ingestions;
using Npgsql;

namespace MedicalAssistance.Ingestion.Api.Tests;

/// <summary>
/// The one fact this service hands back to the backend without a doctor asking:
/// a Session Transcript that failed. The backend owns the Consultation the
/// transcript belongs to, so it — not a doctor polling this service — has to
/// find out. <see cref="IntegrationEventOutboxMessage.IngestionFailed"/> is
/// written in the same transaction as the Failed status
/// (<c>IngestionStore.MarkFailedAsync</c>), and <see cref="IntegrationEventOutboxRelay"/>
/// carries it to the shared event bus. That relay is a separate codebase's
/// consumer contract — <c>IngestionFailedContractTests</c> on the backend pins
/// the JSON it must keep deserializing — so these tests pin this side: the
/// envelope this service actually produces, and when it produces one at all.
/// </summary>
public class IntegrationEventOutboxCharacterizationTests(IngestionApiFixture fixture)
    : IClassFixture<IngestionApiFixture>
{
    private const string Transcript = """
        Doctor: Good morning, what brings you in today?
        Patient: I keep waking up with headaches.
        Doctor: How much water do you usually drink?
        Patient: Maybe one glass a day.
        """;

    private const string ValidPlan = """
        {
          "chunks": [
            { "startLine": 0, "endLine": 1, "contextBlurb": "Complaint discussed." },
            { "startLine": 2, "endLine": 3, "contextBlurb": "Hydration advice." }
          ],
          "summary": "Session summary."
        }
        """;

    [Fact]
    public async Task A_terminal_transcript_failure_writes_the_ingestion_failed_outbox_row()
    {
        // No scripted chat response, so chunking fails and the ingestion
        // reaches Failed on its own — the same trigger StatusEventTests uses.
        var client = fixture.Factory.CreateClient();
        var ingestionId = await PostAsync(client, "pat-outbox-failed");
        await WaitForStatusAsync(client, ingestionId, "Failed");

        var row = await ReadOutboxRowAsync(ingestionId);

        Assert.Equal(IntegrationEventOutboxMessage.IngestionFailedEventType, row.EventType);
        Assert.Null(row.PublishedAt);

        using var body = JsonDocument.Parse(row.Body);
        var payload = body.RootElement.GetProperty("payload");
        Assert.Equal("sess-pat-outbox-failed", payload.GetProperty("sessionId").GetString());
        Assert.Equal(ingestionId, payload.GetProperty("ingestionId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("reason").GetString()));
    }

    [Fact]
    public void The_outbox_envelope_matches_the_backend_s_expected_contract_shape()
    {
        // Built directly, with no HTTP or database involved: this is the
        // producer-side half of the cross-codebase contract the backend's
        // IngestionFailedContractTests pins from the consumer side. Both
        // sides must agree on field names, casing, and null handling without
        // either one importing the other.
        var message = IntegrationEventOutboxMessage.IngestionFailed(
            Guid.NewGuid(), sessionId: "42", reason: "Chunking agent produced an invalid chunk plan");

        using var envelope = JsonDocument.Parse(message.Body);
        var root = envelope.RootElement;

        Assert.Equal(JsonValueKind.String, root.GetProperty("eventId").ValueKind);
        Assert.Equal(
            "clinicalknowledge.ingestion-failed.v1", root.GetProperty("eventType").GetString());
        Assert.Equal(JsonValueKind.Number, root.GetProperty("eventVersion").ValueKind);
        Assert.Equal(1, root.GetProperty("eventVersion").GetInt32());
        Assert.Equal(JsonValueKind.String, root.GetProperty("occurredAtUtc").ValueKind);
        Assert.Equal("clinical-knowledge", root.GetProperty("producer").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("correlationId").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("causationId").ValueKind);

        var payload = root.GetProperty("payload");
        Assert.Equal("42", payload.GetProperty("sessionId").GetString());
        Assert.Equal(message.EventId, root.GetProperty("eventId").GetGuid());
        Assert.Equal(
            "Chunking agent produced an invalid chunk plan", payload.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A_successful_ingestion_writes_no_outbox_row()
    {
        var client = fixture.Factory.CreateClient();
        fixture.ChatClient.EnqueueResponse(ValidPlan);
        var ingestionId = await PostAsync(client, "pat-outbox-completed");
        await WaitForStatusAsync(client, ingestionId, "Completed");

        Assert.False(await OutboxRowExistsAsync(ingestionId));
    }

    [Fact]
    public async Task The_relay_leaves_the_row_unpublished_when_no_broker_is_configured()
    {
        // The fixture never sets RabbitMQ:Host, so IntegrationEventOutboxRelay
        // (already running as a hosted service in every test) stays inert by
        // its own design — see its class summary. Waiting past its 2-second
        // poll interval proves that end to end, through the real service,
        // rather than by inspecting its options.
        var client = fixture.Factory.CreateClient();
        var ingestionId = await PostAsync(client, "pat-outbox-unpublished");
        await WaitForStatusAsync(client, ingestionId, "Failed");

        await Task.Delay(TimeSpan.FromSeconds(3));

        var row = await ReadOutboxRowAsync(ingestionId);
        Assert.Null(row.PublishedAt);
    }

    private static async Task<Guid> PostAsync(HttpClient client, string patientId)
    {
        var response = await client.PostAsJsonAsync("/ingestions", Payload(patientId));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ingestionId").GetGuid();
    }

    private static object Payload(string patientId) => new
    {
        documentType = "SessionTranscript",
        doctorId = "doc-outbox",
        patientId,
        sessionId = $"sess-{patientId}",
        sequenceNumber = 1,
        language = "en",
        transcript = Transcript,
    };

    private static async Task<JsonElement> WaitForStatusAsync(HttpClient client, Guid ingestionId, string expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        var lastSeen = "<never fetched>";
        while (DateTime.UtcNow < deadline)
        {
            var status = await client.GetFromJsonAsync<JsonElement>($"/ingestions/{ingestionId}");
            lastSeen = status.GetRawText();
            if (status.GetProperty("status").GetString() == expected)
                return status;
            await Task.Delay(50);
        }
        Assert.Fail($"Ingestion {ingestionId} never reached {expected}. Last: {lastSeen}");
        throw new UnreachableException();
    }

    private async Task<(string EventType, string Body, DateTimeOffset? PublishedAt)> ReadOutboxRowAsync(
        Guid ingestionId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT event_type, body, published_at FROM clinicalknowledge_outbox " +
            "WHERE body -> 'payload' ->> 'ingestionId' = $1",
            connection);
        command.Parameters.AddWithValue(ingestionId.ToString());
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync(), $"No outbox row found for ingestion {ingestionId}.");
        var row = (
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(2));
        Assert.False(await reader.ReadAsync(), $"More than one outbox row found for ingestion {ingestionId}.");
        return row;
    }

    private async Task<bool> OutboxRowExistsAsync(Guid ingestionId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM clinicalknowledge_outbox " +
            "WHERE body -> 'payload' ->> 'ingestionId' = $1",
            connection);
        command.Parameters.AddWithValue(ingestionId.ToString());
        return (long)(await command.ExecuteScalarAsync())! > 0;
    }
}
