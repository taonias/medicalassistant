using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using MedicalAssistant.Application.Contracts.ClinicalKnowledge;
using MedicalAssistant.Application.Models;
using Microsoft.Extensions.Options;

namespace MedicalAssistant.Infrastructure.ClinicalKnowledge;

public sealed class ClinicalKnowledgeHttpClient :
    ITranscriptIngestionGateway, IClinicalKnowledgeDeletionGateway, IGroundedAnswerGateway, IConversationSummarizer
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

    public async Task<ClinicalKnowledgeAnswer> GetGroundedAnswerAsync(
        ClinicalKnowledgeChatRequest request,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.SubmitTimeoutSeconds));

        var recentTurns = request.RecentTurns?
            .Select(t => new ChatTurnRequest(t.Role, t.Text))
            .ToList();

        var response = await _httpClient.PostAsJsonAsync(
            $"patients/{Uri.EscapeDataString(request.PatientId)}/chat/answer",
            new ChatAnswerRequest(
                request.DoctorId,
                request.Question,
                request.TopK,
                recentTurns,
                request.PriorSummary,
                request.AskId),
            JsonOptions,
            timeout.Token);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ChatAnswerResponse>(JsonOptions, timeout.Token)
            ?? throw new InvalidOperationException("Clinical Knowledge returned an empty chat response.");

        var citations = (body.Citations ?? [])
            .Select(c => new ClinicalKnowledgeCitation(
                c.Label ?? string.Empty,
                c.ChunkId,
                c.DocumentId ?? string.Empty,
                c.DocumentType ?? string.Empty,
                c.SessionId,
                c.DocumentDate,
                c.SourceRef,
                c.Quote ?? string.Empty,
                c.Score))
            .ToArray();

        return new ClinicalKnowledgeAnswer(
            body.Answer ?? string.Empty,
            body.Refused,
            body.RetrievalUsed,
            body.Language ?? string.Empty,
            citations);
    }

    public async Task<string> SummarizeConversationAsync(
        ClinicalKnowledgeSummarizeRequest request,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.SubmitTimeoutSeconds));

        var newTurns = request.NewTurns
            .Select(t => new ChatTurnRequest(t.Role, t.Text))
            .ToList();

        var response = await _httpClient.PostAsJsonAsync(
            $"patients/{Uri.EscapeDataString(request.PatientId)}/chat/summarize",
            new ChatSummarizeRequest(request.PriorSummary, newTurns),
            JsonOptions,
            timeout.Token);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ChatSummarizeResponse>(JsonOptions, timeout.Token);
        return body?.Summary ?? string.Empty;
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

    private sealed record ChatAnswerRequest(
        string DoctorId,
        string Question,
        int TopK,
        List<ChatTurnRequest>? RecentTurns,
        string? PriorSummary,
        Guid? AskId);

    private sealed record ChatTurnRequest(string Role, string Text);

    private sealed record ChatSummarizeRequest(string? PriorSummary, List<ChatTurnRequest> NewTurns);

    private sealed record ChatSummarizeResponse(string? Summary);

    private sealed record ChatAnswerResponse(
        string? Answer,
        bool Refused,
        bool RetrievalUsed,
        string? Language,
        List<ChatCitationResponse>? Citations);

    private sealed record ChatCitationResponse(
        string? Label,
        Guid ChunkId,
        string? DocumentId,
        string? DocumentType,
        string? SessionId,
        DateTimeOffset? DocumentDate,
        string? SourceRef,
        string? Quote,
        double Score);
}
