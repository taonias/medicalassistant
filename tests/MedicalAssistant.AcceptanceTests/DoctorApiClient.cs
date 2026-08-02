using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace MedicalAssistant.AcceptanceTests;

public sealed class DoctorApiClient
{
    private readonly HttpClient _http;

    private DoctorApiClient(HttpClient http) => _http = http;

    internal static async Task<DoctorApiClient> RegisterAsync(
        HttpClient http,
        CancellationToken cancellationToken)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var userName = $"doctor-{suffix}";
        var password = "acceptance-pass";
        using var registration = await http.PostAsJsonAsync("/api/Auth/register", new
        {
            email = $"{userName}@example.test",
            userName,
            password,
            firstName = "Synthetic",
            lastName = "Doctor",
        }, cancellationToken);
        EnsureSuccess(registration);

        using var login = await http.PostAsJsonAsync("/api/Auth/login", new { userName, password }, cancellationToken);
        EnsureSuccess(login);
        var authentication = await login.Content.ReadFromJsonAsync<AuthenticationView>(cancellationToken)
            ?? throw new InvalidOperationException("The backend returned an empty authentication response.");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authentication.Token);
        return new DoctorApiClient(http);
    }

    public async Task<ConsultationView> CreateConsultationAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync("/api/Consultation", new
        {
            consultationDate = DateTime.UtcNow,
            idempotencyKey = Guid.NewGuid().ToString("N"),
        }, cancellationToken);
        EnsureSuccess(response);
        return await ReadConsultationAsync(response, cancellationToken);
    }

    public async Task<ConsultationView> UploadAudioAsync(
        int consultationId,
        SyntheticRecording recording,
        CancellationToken cancellationToken = default)
    {
        using var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(recording.Content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(recording.ContentType);
        multipart.Add(file, "audioFile", recording.FileName);
        multipart.Add(new StringContent("1"), "durationSeconds");
        using var response = await _http.PostAsync($"/api/Consultation/{consultationId}/audio", multipart, cancellationToken);
        EnsureSuccess(response);
        return await ReadConsultationAsync(response, cancellationToken);
    }

    public async Task<ConsultationView> GetConsultationAsync(
        int consultationId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync($"/api/Consultation/{consultationId}", cancellationToken);
        EnsureSuccess(response);
        return await ReadConsultationAsync(response, cancellationToken);
    }

    public async Task<TranscriptView> WaitForTranscriptAsync(
        int consultationId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return await Eventual.UntilNotNullAsync(async token =>
        {
            using var response = await _http.GetAsync($"/api/Transcript/{consultationId}", token);
            EnsureSuccess(response);
            return await response.Content.ReadFromJsonAsync<TranscriptView>(token);
        }, timeout, cancellationToken);
    }

    private static async Task<ConsultationView> ReadConsultationAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<ConsultationView>(cancellationToken)
        ?? throw new InvalidOperationException("The backend returned an empty Consultation response.");

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;
        var path = response.RequestMessage?.RequestUri?.AbsolutePath ?? "<unknown>";
        throw new HttpRequestException(
            $"Backend request to {path} failed with {(int)response.StatusCode} {response.ReasonPhrase}.",
            null,
            response.StatusCode);
    }

    private sealed record AuthenticationView(string Token);
}

public sealed record ConsultationView(
    int Id,
    string Status,
    string? AudioBlobUri,
    string? AudioContentType);

public sealed record TranscriptView(
    int Id,
    int ConsultationId,
    string Status,
    string? Transcript,
    DateTime? ProcessedAt,
    string? FailureReason);
