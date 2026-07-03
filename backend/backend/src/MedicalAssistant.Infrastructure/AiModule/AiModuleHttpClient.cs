using MedicalAssistant.Application.Contracts.AiModule;
using MedicalAssistant.Application.Models;
using MedicalAssistant.Application.Models.AiModule;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace MedicalAssistant.Infrastructure.AiModule;

public class AiModuleHttpClient : IAiModuleClient
{
    private readonly HttpClient _httpClient;
    private readonly AiModuleSettings _settings;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AiModuleHttpClient(HttpClient httpClient, IOptions<AiModuleSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _settings.ApiKey);
    }

    public async Task<TranscriptionJobResponse> StartTranscriptionAsync(TranscriptionJobRequest request, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_settings.TranscriptionSubmitTimeoutSeconds));

        var response = await _httpClient.PostAsJsonAsync("v1/transcribe", request, cts.Token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TranscriptionJobResponse>(JsonOptions, cts.Token))!;
    }

    public async Task<TranscriptionStatusResponse> GetTranscriptionStatusAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"v1/transcribe/{jobId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TranscriptionStatusResponse>(JsonOptions, cancellationToken))!;
    }

    public async Task<StructuredDataJobResponse> ExtractStructuredDataAsync(StructuredDataJobRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("v1/extract", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StructuredDataJobResponse>(JsonOptions, cancellationToken))!;
    }

    public async Task<IndexDocumentsResponse> IndexDocumentsAsync(IndexDocumentsJobRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("v1/index/documents", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IndexDocumentsResponse>(JsonOptions, cancellationToken))!;
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_settings.ChatTimeoutSeconds));

        var response = await _httpClient.PostAsJsonAsync("v1/chat", request, cts.Token);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions, cts.Token))!;
    }

    public async Task<ActionJobResponse> TriggerActionAsync(ActionJobRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"v1/actions/{request.ActionType}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ActionJobResponse>(JsonOptions, cancellationToken))!;
    }

    public async Task<ActionStatusResponse> GetActionStatusAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"v1/actions/{jobId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ActionStatusResponse>(JsonOptions, cancellationToken))!;
    }
}
