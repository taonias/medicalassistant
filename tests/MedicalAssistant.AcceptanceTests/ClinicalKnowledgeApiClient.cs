using System.Net.Http.Json;

namespace MedicalAssistant.AcceptanceTests;

/// <summary>Observes Clinical Knowledge only through its authenticated HTTP interface.</summary>
public sealed class ClinicalKnowledgeApiClient : IDisposable
{
    private readonly HttpClient _http;

    public ClinicalKnowledgeApiClient(HttpClient http, string apiKey)
    {
        _http = http;
        _http.DefaultRequestHeaders.Remove("X-Api-Key");
        _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public async Task<ClinicalKnowledgeIngestionView> WaitForCompletedIngestionAsync(
        Guid ingestionId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return await Eventual.UntilAsync(async token =>
        {
            var state = await GetIngestionAsync(ingestionId, token);
            if (string.Equals(state.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Clinical Knowledge Ingestion {ingestionId} failed.");
            return string.Equals(state.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                ? state
                : null;
        }, timeout, cancellationToken);
    }

    public async Task<Guid> SubmitTranscriptAsync(
        string doctorId,
        string patientId,
        string sessionId,
        string transcript,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("/ingestions", new
        {
            documentType = "SessionTranscript",
            doctorId,
            patientId,
            sessionId,
            sequenceNumber = 1,
            sessionDate = DateTimeOffset.UtcNow,
            language = "en",
            transcript,
        }, cancellationToken);
        EnsureSuccess(response);
        var accepted = await response.Content.ReadFromJsonAsync<IngestionAcceptedView>(cancellationToken)
            ?? throw new InvalidOperationException("Clinical Knowledge returned an empty acceptance response.");
        return accepted.IngestionId;
    }

    public async Task<ClinicalKnowledgeIngestionView> GetIngestionAsync(
        Guid ingestionId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"/ingestions/{ingestionId}", cancellationToken);
        EnsureSuccess(response);
        return await response.Content.ReadFromJsonAsync<ClinicalKnowledgeIngestionView>(cancellationToken)
            ?? throw new InvalidOperationException("Clinical Knowledge returned an empty Ingestion response.");
    }

    public void Dispose() => _http.Dispose();

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;
        var path = response.RequestMessage?.RequestUri?.AbsolutePath ?? "<unknown>";
        throw new HttpRequestException(
            $"Clinical Knowledge request to {path} failed with {(int)response.StatusCode} {response.ReasonPhrase}.",
            null,
            response.StatusCode);
    }
}

public sealed record ClinicalKnowledgeIngestionView(
    Guid IngestionId,
    string Status,
    string? ErrorMessage,
    string? Summary);

internal sealed record IngestionAcceptedView(Guid IngestionId, bool Duplicate);
