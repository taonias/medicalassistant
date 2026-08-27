using System.Net;
using System.Text.Json;
using MedicalAssistant.Api.Realtime;
using MedicalAssistant.Application.Modules.Assistance.Chat;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace MedicalAssistant.AcceptanceTests.Contracts;

public sealed class BackendRealtimeContractTests
{
    [Fact]
    public async Task Chat_hub_remains_available_at_the_doctor_facing_negotiate_route()
    {
        await using var factory = new BackendContractApiFactory();
        using var client = factory.CreateClient(new()
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

        using var response = await client.PostAsync("/hubs/chat/negotiate?negotiateVersion=1", null);
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(payload.RootElement.GetProperty("connectionId").GetString()));
        Assert.NotEmpty(payload.RootElement.GetProperty("availableTransports").EnumerateArray());
    }

    [Fact]
    public async Task Chat_progress_event_keeps_its_client_method_target_and_payload_shape()
    {
        var client = new Mock<IClientProxy>(MockBehavior.Strict);
        var clients = new Mock<IHubClients>(MockBehavior.Strict);
        clients.Setup(candidate => candidate.User("doctor-7")).Returns(client.Object);
        var hub = new Mock<IHubContext<ChatHub>>(MockBehavior.Strict);
        hub.SetupGet(candidate => candidate.Clients).Returns(clients.Object);
        var occurredAt = new DateTimeOffset(2026, 8, 23, 12, 30, 0, TimeSpan.Zero);
        var progress = new ChatProgressEvent(
            Guid.Parse("fc5f18d2-b4c8-4e51-ae15-69340217b9bb"),
            "retrieving",
            "Searching patient evidence",
            occurredAt);
        client
            .Setup(candidate => candidate.SendCoreAsync(
                "ChatProgress",
                It.Is<object?[]>(arguments =>
                    arguments.Length == 1 &&
                    ReferenceEquals(arguments[0], progress)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var notifier = new SignalRChatProgressNotifier(
            hub.Object,
            NullLogger<SignalRChatProgressNotifier>.Instance);

        await notifier.NotifyAsync("doctor-7", progress);

        Assert.Equal("ChatProgress", ChatHub.ClientMethod);
        Assert.Equal(
            "{\"askId\":\"fc5f18d2-b4c8-4e51-ae15-69340217b9bb\",\"phase\":\"retrieving\",\"message\":\"Searching patient evidence\",\"occurredAt\":\"2026-08-23T12:30:00+00:00\"}",
            JsonSerializer.Serialize(progress, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        clients.VerifyAll();
        hub.VerifyAll();
        client.VerifyAll();
    }
}
