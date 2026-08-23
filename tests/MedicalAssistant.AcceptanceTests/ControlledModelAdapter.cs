using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MedicalAssistant.AcceptanceTests;

/// <summary>
/// Scripts the model responses used by Clinical Knowledge while its HTTP host,
/// serialization, and SDK adapters remain real.
/// </summary>
public sealed class ControlledModelAdapter
{
    private readonly ConcurrentQueue<string> _transcriptPlans = new();

    public void EnqueueTranscriptPlan(string contextBlurb, string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contextBlurb);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        _transcriptPlans.Enqueue(JsonSerializer.Serialize(new
        {
            chunks = new[] { new { startLine = 0, endLine = 1, contextBlurb } },
            summary,
        }));
    }

    internal string NextChatResponse(string requestBody)
    {
        if (requestBody.Contains("rolling clinical overview", StringComparison.OrdinalIgnoreCase))
            return "Controlled rolling patient overview.";

        if (!_transcriptPlans.TryDequeue(out var response))
            throw new InvalidOperationException("No controlled model response has been scripted.");
        return response;
    }
}

/// <summary>
/// Local OpenAI-compatible provider boundary shared by the real Worker and
/// Clinical Knowledge processes. No application service is replaced in DI.
/// </summary>
internal sealed class ControlledProviderService : IAsyncDisposable
{
    private const int EmbeddingDimensions = 3072;
    private readonly ControlledSpeechAdapter _speech;
    private readonly ControlledModelAdapter _models;
    private WebApplication? _application;

    public ControlledProviderService(ControlledSpeechAdapter speech, ControlledModelAdapter models)
    {
        _speech = speech;
        _models = models;
    }

    public Uri Endpoint { get; private set; } = null!;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_application is not null)
            return;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://0.0.0.0:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();

        app.MapPost("/v1/audio/transcriptions", async (HttpRequest request, CancellationToken token) =>
        {
            var form = await request.ReadFormAsync(token);
            var file = form.Files.GetFile("file")
                ?? throw new BadHttpRequestException("The controlled speech request did not contain audio.");
            await using var audio = file.OpenReadStream();
            using var copy = new MemoryStream();
            await audio.CopyToAsync(copy, token);
            var transcript = await _speech.TranscribeAsync(
                new SyntheticRecording(copy.ToArray(), file.FileName, file.ContentType),
                token);
            return Results.Json(new { text = transcript });
        });

        app.MapPost("/v1/chat/completions", async (HttpRequest request, CancellationToken token) =>
        {
            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync(token);
            var response = _models.NextChatResponse(body);
            return Results.Json(new
            {
                id = $"chatcmpl-{Guid.NewGuid():N}",
                @object = "chat.completion",
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                model = "controlled-chat",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new { role = "assistant", content = response },
                        finish_reason = "stop",
                    },
                },
                usage = new { prompt_tokens = 1, completion_tokens = 1, total_tokens = 2 },
            });
        });

        app.MapPost("/v1/embeddings", async (HttpRequest request, CancellationToken token) =>
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: token);
            var input = document.RootElement.GetProperty("input");
            var count = input.ValueKind == JsonValueKind.Array ? input.GetArrayLength() : 1;
            var data = Enumerable.Range(0, count)
                .Select(index => new
                {
                    @object = "embedding",
                    index,
                    embedding = DeterministicEmbedding(index),
                });
            return Results.Json(new
            {
                @object = "list",
                data,
                model = "controlled-embedding",
                usage = new { prompt_tokens = count, total_tokens = count },
            });
        });

        await app.StartAsync(cancellationToken);
        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses;
        var boundEndpoint = new Uri(addresses?.Single()
            ?? throw new InvalidOperationException("The controlled provider did not publish an endpoint."));
        Endpoint = new UriBuilder(boundEndpoint) { Host = "127.0.0.1" }.Uri;
        _application = app;
    }

    public async ValueTask DisposeAsync()
    {
        if (_application is null)
            return;
        await _application.StopAsync();
        await _application.DisposeAsync();
        _application = null;
    }

    private static float[] DeterministicEmbedding(int inputIndex)
    {
        var vector = new float[EmbeddingDimensions];
        vector[inputIndex % EmbeddingDimensions] = 1f;
        return vector;
    }
}
