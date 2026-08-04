using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using MedicalAssistant.Application.Contracts.ClinicalKnowledge;
using MedicalAssistant.Application.Models;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Infrastructure.ClinicalKnowledge;

public sealed class ClinicalKnowledgeHttpClient : IClinicalKnowledgeClient
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ClinicalKnowledgeSettings _settings;

    public ClinicalKnowledgeHttpClient(
        HttpClient httpClient,
        IOptions<ClinicalKnowledgeSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Remove(ApiKeyHeaderName);
        _httpClient.DefaultRequestHeaders.Add(ApiKeyHeaderName, _settings.ApiKey);
    }

    public async Task<ClinicalKnowledgeIngestionAccepted> SubmitSessionTranscriptAsync(
        ClinicalKnowledgeSessionTranscriptRequest request,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.SubmitTimeoutSeconds));

        var response = await _httpClient.PostAsJsonAsync(
            "ingestions",
            new SessionTranscriptIngestionRequest(
                DocumentType: request.DocumentType,
                request.DoctorId,
                request.PatientId,
                request.SessionId,
                request.SequenceNumber,
                request.SessionDate,
                request.Language,
                request.Transcript),
            JsonOptions,
            timeout.Token);

        response.EnsureSuccessStatusCode();
        var accepted = await response.Content.ReadFromJsonAsync<ClinicalKnowledgeIngestionAccepted>(
            JsonOptions,
            timeout.Token);

        return accepted
            ?? throw new InvalidOperationException("Clinical Knowledge returned an empty ingestion response.");
    }

    public async Task<ClinicalKnowledgeUnIngestResult> UnIngestDocumentAsync(
        string documentId,
        string removedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(removedBy);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.SubmitTimeoutSeconds));

        var requestUri =
            $"documents/{Uri.EscapeDataString(documentId)}?removedBy={Uri.EscapeDataString(removedBy)}";
        var response = await _httpClient.DeleteAsync(requestUri, timeout.Token);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ClinicalKnowledgeUnIngestResult(
                documentId,
                ClinicalKnowledgeUnIngestStatus.AlreadyMissing);
        }

        response.EnsureSuccessStatusCode();
        return new ClinicalKnowledgeUnIngestResult(
            documentId,
            ClinicalKnowledgeUnIngestStatus.Removed);
    }

    private sealed record SessionTranscriptIngestionRequest(
        string DocumentType,
        string DoctorId,
        string PatientId,
        string SessionId,
        int SequenceNumber,
        DateTimeOffset? SessionDate,
        string? Language,
        string Transcript);
}
